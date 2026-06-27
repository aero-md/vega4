# Deploy VEGA (bot Discord .NET) vers un hôte Linux via SSH — TEMPLATE.
#
# Copiez ce fichier vers `deploy.ps1` (ignoré par git) et renseignez vos valeurs :
# hôte SSH, chemin distant, nom du service systemd, runtime cible.
#
# Calqué sur redsunsbio, adapté à .NET : on publie un binaire self-contained
# (aucun runtime .NET requis sur l'hôte).
#
# Migrations : appliquées AUTOMATIQUEMENT à chaque déploiement (toutes les
# database/migrations/*.sql dans l'ordre, via psql sur l'hôte — voir scripts/migrate.sh).
# Idempotentes (CREATE ... IF NOT EXISTS), donc rejouables sans risque.
# Utiliser -SkipMigrate pour les sauter exceptionnellement.
#
# Prérequis côté hôte : psql, un service systemd, et un appsettings.json (token + connexionString)
# posé manuellement — jamais poussé ni écrasé par ce script. Le bootstrap de base
# (database/createdb.sql) se joue à la main une fois, hors de ce script.
#
# Usage :
#   .\deploy.ps1                        # publish + deploy + migrations + restart service
#   .\deploy.ps1 -SkipBuild             # deploy seul (le dossier publish doit exister)
#   .\deploy.ps1 -SkipMigrate           # ne pas appliquer les migrations
#   .\deploy.ps1 -PiHost user@host      # hôte alternatif

param(
    [string]$PiHost = "user@your-server",
    [string]$RemotePath = "/srv/vega",
    [string]$ServiceName = "vega",
    [string]$Runtime = "linux-arm64",
    [switch]$SkipBuild,
    [switch]$SkipMigrate
)

$ErrorActionPreference = "Stop"
Set-Location $PSScriptRoot

$project = "VEGA/VEGA.csproj"
$publishDir = "VEGA/bin/Release/net9.0/$Runtime/publish"

if (-not $SkipBuild) {
    Write-Host "==> dotnet publish ($Runtime, self-contained)" -ForegroundColor Cyan
    # -p:UseAppHost garantit l'apphost natif 'VEGA' lancé par systemd.
    dotnet publish $project -c Release -r $Runtime --self-contained true -p:UseAppHost=true -o $publishDir
    if ($LASTEXITCODE -ne 0) { throw "Publish a échoué" }
}

if (-not (Test-Path "$publishDir/VEGA")) {
    throw "Binaire publié introuvable ($publishDir/VEGA) - lance la publication avant ou retire -SkipBuild"
}

# Entrées indispensables à embarquer (chemins relatifs au repo).
# database/ = createdb.sql (bootstrap manuel, non joué par le runner) + migrations/ (rejouées).
foreach ($p in @("database/migrations", "scripts/migrate.sh")) {
    if (-not (Test-Path $p)) { throw "Entrée manquante : $p" }
}

# On assemble un dossier de staging pour contrôler le layout du tarball et exclure
# appsettings.json (secrets) qui ne doit JAMAIS écraser celui de l'hôte.
$staging = Join-Path ([System.IO.Path]::GetTempPath()) "vega-staging-$([guid]::NewGuid().ToString('N'))"
$tar = New-TemporaryFile
$tarPath = $tar.FullName

try {
    New-Item -ItemType Directory -Path $staging -Force | Out-Null

    Write-Host "==> Préparation du staging" -ForegroundColor Cyan
    Copy-Item "$publishDir/*" $staging -Recurse -Force
    Remove-Item (Join-Path $staging "appsettings.json") -ErrorAction SilentlyContinue
    # database/ (createdb.sql + migrations/) ; le runner ne jouera que database/migrations/.
    Copy-Item "database" $staging -Recurse -Force
    New-Item -ItemType Directory -Path (Join-Path $staging "scripts") -Force | Out-Null
    Copy-Item "scripts/migrate.sh" (Join-Path $staging "scripts") -Force

    Write-Host "==> Création du tarball" -ForegroundColor Cyan
    tar -czf $tarPath -C $staging .
    if ($LASTEXITCODE -ne 0) { throw "tar a échoué" }

    $size = [math]::Round((Get-Item $tarPath).Length / 1MB, 1)
    Write-Host "    -> $size Mo compressés" -ForegroundColor DarkGray

    Write-Host "==> Upload vers ${PiHost}:${RemotePath}" -ForegroundColor Cyan
    scp $tarPath "${PiHost}:/tmp/vega-deploy.tar.gz"
    if ($LASTEXITCODE -ne 0) { throw "scp a échoué" }

    Write-Host "==> Extraction distante" -ForegroundColor Cyan
    # On préserve appsettings.json (secrets) et logs/ existants.
    $remoteScript = @"
set -e
mkdir -p '$RemotePath'
cd '$RemotePath'
# wipe sauf appsettings.json et logs/
find . -mindepth 1 -maxdepth 1 ! -name appsettings.json ! -name logs -exec rm -rf {} +
tar -xzf /tmp/vega-deploy.tar.gz -C '$RemotePath'
rm /tmp/vega-deploy.tar.gz
chmod +x ./VEGA ./scripts/migrate.sh
if [ ! -f appsettings.json ]; then
  echo '[deploy] ATTENTION : appsettings.json absent — copie appsettings-example.json et renseigne token + connexionString.' >&2
fi
"@
    ssh $PiHost $remoteScript
    if ($LASTEXITCODE -ne 0) {
        Write-Host ""
        Write-Host "Hint: si 'Permission denied', lance côté hôte :" -ForegroundColor Yellow
        Write-Host "      sudo chown -R $($PiHost.Split('@')[0]):$($PiHost.Split('@')[0]) $RemotePath" -ForegroundColor Yellow
        Write-Host ""
        throw "Extraction distante a échoué"
    }

    # Migrations AVANT le restart : le schéma est prêt quand le nouveau code prend le trafic.
    if (-not $SkipMigrate) {
        Write-Host "==> Application des migrations (psql, appsettings.json distant)" -ForegroundColor Cyan
        ssh $PiHost "cd '$RemotePath' && bash scripts/migrate.sh"
        if ($LASTEXITCODE -ne 0) { throw "Migration distante a échoué" }
    }
    else {
        Write-Host "==> Migrations ignorées (-SkipMigrate)" -ForegroundColor DarkGray
    }

    Write-Host "==> Restart service systemd '$ServiceName'" -ForegroundColor Cyan
    ssh $PiHost "sudo systemctl restart $ServiceName && sudo systemctl is-active $ServiceName"
    if ($LASTEXITCODE -ne 0) { throw "Restart service a échoué" }

    Write-Host "==> Done." -ForegroundColor Green
}
finally {
    Remove-Item $tarPath -ErrorAction SilentlyContinue
    Remove-Item $staging -Recurse -Force -ErrorAction SilentlyContinue
}
