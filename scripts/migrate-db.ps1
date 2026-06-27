<#
.SYNOPSIS
  Sync production data from SmarterASP SQL Server into the Railway Postgres DB
  with pgloader. Everything runs in Docker, so there's nothing to install.

.DESCRIPTION
  Reads both connection strings from environment variables so the secrets stay
  out of this file AND out of your shell history:

    $env:MSSQL_URL = "mssql://USER:PASS@SQL5063.site4now.net/db_a4eac4_lopezautodata"
    $env:PG_URL    = "postgresql://postgres:PASS@HOST.proxy.rlwy.net:PORT/railway"

  PG_URL must be Railway's PUBLIC Postgres URL (the Postgres service's
  DATABASE_PUBLIC_URL / *.proxy.rlwy.net), NOT postgres.railway.internal --
  the internal host isn't reachable from your machine.

  The import:
    - copies DATA ONLY into the schema the app already migrated on startup,
    - quotes identifiers so case-sensitive EF table names ("Cars") match,
    - maps the SQL Server 'dbo' schema onto Postgres 'public',
    - imports only the business tables (auth/migration tables are skipped),
    - TRUNCATEs those tables first so the imported rows replace the seed rows
      the app inserts on startup (avoids primary-key collisions),
    - resets sequences so new inserts don't collide with imported ids.

  All SQL runs from mounted .sql files (not psql -c), because PowerShell strips
  the embedded double-quotes that case-sensitive identifiers need.

.PARAMETER ResetSchema
  DESTRUCTIVE. Drops & recreates the Railway 'public' schema, then STOPS so you
  can redeploy the app on Railway (which re-creates the empty schema + reseeds
  the admin). After it redeploys, re-run this script WITHOUT -ResetSchema to
  import. Use this for the real cutover so the import starts clean.

.PARAMETER SkipVerify
  Skip the post-import row-count check.

.EXAMPLE
  $env:MSSQL_URL = "mssql://user:pw@SQL5063.site4now.net/db_a4eac4_lopezautodata"
  $env:PG_URL    = "postgresql://postgres:pw@viaduct.proxy.rlwy.net:45692/railway"
  ./scripts/migrate-db.ps1
#>
[CmdletBinding()]
param(
    [switch]$ResetSchema,
    [switch]$SkipVerify
)

$ErrorActionPreference = "Stop"

# Business tables to import. Verified against the live schema on `master`: these
# are the exact EF table names (note 'Address' is singular). The auth/Identity
# tables, IdentityServer tables (DeviceCodes/Keys/PersistedGrants), and EF
# migration history are skipped. UserAccounts is excluded on purpose -- it only
# gates NON-admin access (the single admin bypasses it) and references old user
# ids that aren't migrated.
$tables = @('Cars', 'Sales', 'Payments', 'Accounts', 'Lienholders', 'Pictures', 'Address')

function Require-Env($name) {
    $val = [Environment]::GetEnvironmentVariable($name)
    if ([string]::IsNullOrWhiteSpace($val)) {
        throw "Environment variable $name is not set. See the header of this script for the format."
    }
    return $val
}

# --- preflight ---
if (-not (Get-Command docker -ErrorAction SilentlyContinue)) {
    throw "Docker is required (pgloader and psql run in containers). Install/start Docker Desktop."
}
$null = docker info 2>&1
if ($LASTEXITCODE -ne 0) {
    throw "Docker is installed but the engine isn't running. Start Docker Desktop, wait until it says 'Engine running', then re-run."
}
$mssql = Require-Env "MSSQL_URL"
$pg    = Require-Env "PG_URL"
if ($pg -match "railway\.internal") {
    throw "PG_URL is the INTERNAL Railway host (postgres.railway.internal), not reachable from here. Use the PUBLIC URL (Postgres service -> Variables -> DATABASE_PUBLIC_URL, host *.proxy.rlwy.net)."
}

# All command/SQL files live here (mounted into the containers), removed at the end.
# The load file holds both passwords, so this dir must not leak.
$work = Join-Path ([System.IO.Path]::GetTempPath()) ("pgmig-" + [guid]::NewGuid().ToString("N"))
New-Item -ItemType Directory -Path $work | Out-Null

function Save-File($name, $text) {
    # UTF-8 without BOM -- a BOM breaks pgloader's parser and can confuse psql.
    [System.IO.File]::WriteAllText((Join-Path $work $name), $text, (New-Object System.Text.UTF8Encoding($false)))
}

function Run-Psql($name) {
    docker run --rm -v "${work}:/data" postgres:16 psql "$pg" -v ON_ERROR_STOP=1 -f "/data/$name"
    if ($LASTEXITCODE -ne 0) { throw "psql failed running $name." }
}

try {
    # --- optional destructive reset ---
    if ($ResetSchema) {
        Write-Host "RESET will DROP ALL DATA in the Railway Postgres 'public' schema." -ForegroundColor Yellow
        if ((Read-Host "Type 'reset' to confirm") -ne "reset") { throw "Aborted." }
        Save-File "reset.sql" "DROP SCHEMA public CASCADE; CREATE SCHEMA public;"
        Run-Psql "reset.sql"
        Write-Host ""
        Write-Host "Schema dropped. NOW redeploy the app on Railway so EF re-creates the schema + reseeds admin," -ForegroundColor Cyan
        Write-Host "then re-run this script WITHOUT -ResetSchema to import." -ForegroundColor Cyan
        return
    }

    # --- clear the target business tables so imported rows replace the seed rows ---
    $quoted = ($tables | ForEach-Object { '"' + $_ + '"' }) -join ', '
    Write-Host "Clearing target tables before import: $quoted" -ForegroundColor Yellow
    Save-File "truncate.sql" "TRUNCATE $quoted RESTART IDENTITY CASCADE;"
    Run-Psql "truncate.sql"

    # --- run pgloader ---
    $likeList = ($tables | ForEach-Object { "'" + $_ + "'" }) -join ', '
    $load = @"
LOAD DATABASE
  FROM    $mssql
  INTO    $pg

WITH      data only, quote identifiers, reset sequences, disable triggers, no foreign keys

INCLUDING ONLY TABLE NAMES LIKE $likeList IN SCHEMA 'dbo'

ALTER SCHEMA 'dbo' RENAME TO 'public';
"@
    Save-File "migrate.load" $load
    # FreeTDS caps large text columns (e.g. JsonData / nvarchar(max)) on read at a small
    # default, truncating them to ~2 KB. Point pgloader's FreeTDS at a config that lifts it.
    Save-File "freetds.conf" "[global]`n    tds version = auto`n    text size = 2147483647`n"
    Write-Host "Running pgloader (SQL Server -> Railway Postgres)..." -ForegroundColor Cyan
    docker run --rm -v "${work}:/data" -e FREETDSCONF=/data/freetds.conf dimitri/pgloader pgloader /data/migrate.load
    if ($LASTEXITCODE -ne 0) { throw "pgloader exited with code $LASTEXITCODE." }
    Write-Host "Import complete." -ForegroundColor Green

    # --- verify ---
    if (-not $SkipVerify) {
        Write-Host "Row counts on Railway Postgres (compare to the SQL Server counts before flipping DNS):" -ForegroundColor Cyan
        $counts = ($tables | ForEach-Object { "SELECT '$_' AS t, count(*) FROM ""$_""" }) -join ' UNION ALL '
        Save-File "verify.sql" "$counts ORDER BY t;"
        Run-Psql "verify.sql"
    }
}
finally {
    # Always remove the work dir -- migrate.load contains both passwords.
    Remove-Item -Recurse -Force $work -ErrorAction SilentlyContinue
}
