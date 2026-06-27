#!/usr/bin/env bash
# Applique toutes les migrations/*.sql dans l'ordre, via psql, une transaction par fichier.
#
# Pourquoi psql plutôt que bun+pg (comme redsunsbio) : VEGA est en .NET, il n'y a pas de
# runtime JS dans le projet. En revanche PostgreSQL tourne sur le Pi (192.168.1.86), donc
# psql y est présent.
#
# Idempotence : toutes les migrations utilisent CREATE ... IF NOT EXISTS → ce runner est
# rejouable sans risque, il tourne à chaque déploiement.
#
# La chaîne de connexion est lue depuis appsettings.json (clé postgres.connexionString,
# format .NET : "Host=...;Port=...;Database=...;Username=...;Password=..."). Pas de jq requis
# (pas garanti sur le Pi) : on parse en grep/sed.
#
# Usage : bash scripts/migrate.sh            (depuis la racine du déploiement)
#         APPSETTINGS=/chemin/appsettings.json MIGRATIONS_DIR=migrations bash scripts/migrate.sh
set -euo pipefail

DIR="${MIGRATIONS_DIR:-database/migrations}"
SETTINGS="${APPSETTINGS:-appsettings.json}"

if [ ! -f "$SETTINGS" ]; then
    echo "[migrate] $SETTINGS introuvable." >&2
    exit 1
fi

# Extrait la valeur brute de "connexionString" (sans jq).
conn=$(grep -oE '"connexionString"[[:space:]]*:[[:space:]]*"[^"]*"' "$SETTINGS" \
       | head -n1 | sed -E 's/.*:[[:space:]]*"(.*)"$/\1/')
if [ -z "$conn" ]; then
    echo "[migrate] postgres.connexionString introuvable dans $SETTINGS." >&2
    exit 1
fi

# Récupère une clé (insensible à la casse) de la chaîne .NET "k=v;k=v;...".
conn_get() {
    printf '%s' "$conn" | tr ';' '\n' \
        | grep -iE "^[[:space:]]*$1[[:space:]]*=" | head -n1 \
        | sed -E 's/^[^=]*=[[:space:]]*//'
}

export PGHOST="$(conn_get Host)"
export PGPORT="$(conn_get Port)"; PGPORT="${PGPORT:-5432}"
export PGDATABASE="$(conn_get Database)"
export PGUSER="$(conn_get Username)"
export PGPASSWORD="$(conn_get Password)"

if [ -z "$PGHOST" ] || [ -z "$PGDATABASE" ] || [ -z "$PGUSER" ]; then
    echo "[migrate] Chaîne de connexion incomplète (Host/Database/Username requis)." >&2
    exit 1
fi

shopt -s nullglob
files=("$DIR"/*.sql)
if [ ${#files[@]} -eq 0 ]; then
    echo "[migrate] Aucune migration trouvée dans $DIR/."
    exit 0
fi
# Ordre lexicographique = ordre d'application voulu (001_, 002_, 003_…).
IFS=$'\n' files=($(sort <<<"${files[*]}")); unset IFS

for f in "${files[@]}"; do
    printf '[migrate] %s … ' "$(basename "$f")"
    # --single-transaction + ON_ERROR_STOP : un fichier qui échoue à mi-chemin est rollback
    # en entier plutôt que de laisser un état partiel.
    psql --no-psqlrc --quiet --set ON_ERROR_STOP=1 --single-transaction -f "$f" >/dev/null
    echo "ok"
done

echo "[migrate] ${#files[@]} migration(s) appliquée(s)."
