-- ============================================================================
-- createdb.sql — BOOTSTRAP MANUEL (one-shot), HORS du runner de déploiement.
-- ============================================================================
-- À jouer UNE fois par environnement, en superutilisateur (postgres), AVANT le
-- premier passage des migrations. scripts/migrate.sh ne joue PAS ce fichier : il
-- ne touche qu'à database/migrations/.
--
-- Les TABLES ne sont volontairement PAS ici — elles vivent dans
-- database/migrations/*.sql (source de vérité du schéma). Ce fichier se limite à
-- provisionner la base, le rôle applicatif et les droits.
--
-- Remplace <DB_NAME> / <DB_USER> / <DB_PASSWORD> puis :
--   sudo -u postgres psql -f createdb.sql
--   (prod : vega_main / vega_main ; dev : vega_unstable / vega_unstable)
-- ============================================================================

-- 1) Rôle applicatif
CREATE ROLE "<DB_USER>" WITH LOGIN PASSWORD '<DB_PASSWORD>';

-- 2) Base, possédée par le rôle applicatif
CREATE DATABASE "<DB_NAME>"
    WITH OWNER = "<DB_USER>"
    ENCODING = 'UTF8'
    TEMPLATE = template0
    LC_COLLATE = 'en_US.utf8'
    LC_CTYPE = 'en_US.utf8';

-- 3) Le reste se joue DANS la nouvelle base
\connect "<DB_NAME>"

-- 4) Extension requise par les migrations (gen_random_uuid())
CREATE EXTENSION IF NOT EXISTS pgcrypto;

-- 5) Droits sur le schéma public.
--    Depuis PostgreSQL 15, le rôle PUBLIC n'a plus CREATE sur public : sans ces
--    lignes, les migrations échouent avec "permission denied for schema public".
ALTER SCHEMA public OWNER TO "<DB_USER>";
GRANT ALL ON SCHEMA public TO "<DB_USER>";

-- Tables, index et contraintes : voir database/migrations/*.sql (appliqués par le runner).
