<#
.SYNOPSIS
  Copy ONLY the images for the active lot (listed cars) and active payment plans
  (accounts with a remaining balance) from SmarterASP (FTP) straight into
  Cloudflare R2, preserving the 'Images/' key prefix so they match the DB.

.DESCRIPTION
  Queries the (already-imported) Railway Postgres for the exact image keys of:
    - listed cars (active lot), and
    - cars whose account still owes money (InitialDue - sum(payments) > 0).
  Full-size images only -- the separate '.thumbnail.' files are skipped, since the
  app now uses on-the-fly Cloudflare resizing. Then rclone copies just those files
  FTP -> R2 (nothing downloaded locally, nothing to install beyond Docker).
  rclone's Cloudflare provider avoids the R2 checksum/signing issues.

  Env vars:
    $env:PG_URL        = Railway PUBLIC Postgres URL (DATABASE_PUBLIC_URL)
    $env:FTP_HOST      = SmarterASP FTP host
    $env:FTP_USER      = SmarterASP FTP username
    $env:FTP_PASS      = SmarterASP FTP password (plain; obscured for rclone here)
    $env:FTP_ROOT      = (optional) FTP dir that CONTAINS the Images folder,
                         e.g. "" (login root, default) or "wwwroot"
    $env:FTP_TLS       = (optional) "true" to use explicit FTPS
    $env:R2_ENDPOINT   = "https://e8cc1bebfc6496587619b08750451044.r2.cloudflarestorage.com"
    $env:R2_BUCKET     = "<your bucket>"
    $env:R2_ACCESS_KEY = "<access key id>"
    $env:R2_SECRET_KEY = "<secret access key>"

.PARAMETER KeysOnly
  Just print the list of image keys that WOULD be copied, then stop. Use this to
  sanity-check the count before transferring.

.EXAMPLE
  $env:PG_URL="postgresql://postgres:pw@viaduct.proxy.rlwy.net:45692/railway"
  $env:FTP_HOST="..."; $env:FTP_USER="..."; $env:FTP_PASS="..."
  $env:R2_ENDPOINT="https://e8cc...r2.cloudflarestorage.com"; $env:R2_BUCKET="..."
  $env:R2_ACCESS_KEY="..."; $env:R2_SECRET_KEY="..."
  ./scripts/migrate-images.ps1 -KeysOnly     # preview
  ./scripts/migrate-images.ps1               # transfer
#>
[CmdletBinding()]
param([switch]$KeysOnly)

$ErrorActionPreference = "Stop"

function Require-Env($name) {
    $val = [Environment]::GetEnvironmentVariable($name)
    if ([string]::IsNullOrWhiteSpace($val)) { throw "Environment variable $name is not set. See the header of this script." }
    return $val
}
function Opt-Env($name, $default) {
    $val = [Environment]::GetEnvironmentVariable($name)
    if ([string]::IsNullOrWhiteSpace($val)) { return $default }
    return $val
}

if (-not (Get-Command docker -ErrorAction SilentlyContinue)) { throw "Docker is required (psql + rclone run in containers)." }
$null = docker info 2>&1
if ($LASTEXITCODE -ne 0) { throw "Docker is installed but the engine isn't running. Start Docker Desktop and retry." }

$pg = Require-Env "PG_URL"
if ($pg -match "railway\.internal") {
    throw "PG_URL is the INTERNAL Railway host (postgres.railway.internal), not reachable from here. Use the PUBLIC URL -- same user/password, host *.proxy.rlwy.net (Postgres service -> Variables -> DATABASE_PUBLIC_URL)."
}
if (-not $KeysOnly) {
    $r2endpoint = Require-Env "R2_ENDPOINT"
    $r2bucket   = Require-Env "R2_BUCKET"
    $r2access   = Require-Env "R2_ACCESS_KEY"
    $r2secret   = Require-Env "R2_SECRET_KEY"
    $ftphost    = Require-Env "FTP_HOST"
    $ftpuser    = Require-Env "FTP_USER"
    $ftppass    = Require-Env "FTP_PASS"
    $ftproot    = Opt-Env "FTP_ROOT" ""
    $ftptls     = Opt-Env "FTP_TLS" ""
}

$work = Join-Path ([System.IO.Path]::GetTempPath()) ("imgmig-" + [guid]::NewGuid().ToString("N"))
New-Item -ItemType Directory -Path $work | Out-Null

try {
    # --- 1) get the exact keys for active lot + active payments from Postgres ---
    $query = @'
WITH active_cars AS (
    SELECT "Id" FROM "Cars" WHERE "IsListed" = true
    UNION
    SELECT s."CarId"
    FROM "Accounts" a
    JOIN "Sales" s ON s."Id" = a."SaleId"
    WHERE a."InitialDue" - COALESCE((SELECT SUM(p."Amount") FROM "Payments" p WHERE p."AccountId" = a."Id"), 0) > 0
),
active_pics AS (
    -- Full-size images only. The separate '.thumbnail.' files are intentionally NOT
    -- copied: the grid now uses on-the-fly Cloudflare resizing of the full image.
    -- FTP login is the Images folder itself, so strip the 'Images/' prefix; the
    -- R2 destination (r2:<bucket>/Images) puts it back as the object key.
    SELECT regexp_replace(replace("URL", '\', '/'), '^Images/', '', 'i') AS key
    FROM "Pictures"
    WHERE "CarId" IN (SELECT "Id" FROM active_cars)
)
SELECT key FROM active_pics ORDER BY 1;
'@
    [System.IO.File]::WriteAllText((Join-Path $work "keys.sql"), $query, (New-Object System.Text.UTF8Encoding($false)))
    docker run --rm -v "${work}:/data" postgres:16 psql "$pg" -v ON_ERROR_STOP=1 -t -A -o /data/keys.txt -f /data/keys.sql
    if ($LASTEXITCODE -ne 0) { throw "Postgres query failed." }

    $keys = @(Get-Content (Join-Path $work "keys.txt") | Where-Object { $_.Trim() -ne "" })
    Write-Host "$($keys.Count) image keys for active lot + active payments." -ForegroundColor Cyan
    if ($KeysOnly) { $keys | ForEach-Object { Write-Host "  $_" }; return }
    if ($keys.Count -eq 0) { throw "No keys found -- nothing to copy. (Is the data imported? Are any cars listed / unpaid?)" }

    # --- 2) rclone copy just those files, FTP -> R2 ---
    $ftppassObs = (docker run --rm rclone/rclone obscure "$ftppass").Trim()
    $rc = @(
        "-e", "RCLONE_CONFIG_FTP_TYPE=ftp",
        "-e", "RCLONE_CONFIG_FTP_HOST=$ftphost",
        "-e", "RCLONE_CONFIG_FTP_USER=$ftpuser",
        "-e", "RCLONE_CONFIG_FTP_PASS=$ftppassObs",
        "-e", "RCLONE_CONFIG_R2_TYPE=s3",
        "-e", "RCLONE_CONFIG_R2_PROVIDER=Cloudflare",
        "-e", "RCLONE_CONFIG_R2_ACCESS_KEY_ID=$r2access",
        "-e", "RCLONE_CONFIG_R2_SECRET_ACCESS_KEY=$r2secret",
        "-e", "RCLONE_CONFIG_R2_ENDPOINT=$r2endpoint"
    )
    if ($ftptls -eq "true") { $rc += @("-e", "RCLONE_CONFIG_FTP_EXPLICIT_TLS=true") }

    Write-Host "Copying $($keys.Count) files FTP -> r2:$r2bucket/Images ..." -ForegroundColor Cyan
    docker run --rm -v "${work}:/data" @rc rclone/rclone copy "ftp:$ftproot" "r2:$r2bucket/Images" --files-from /data/keys.txt --progress --transfers 8
    if ($LASTEXITCODE -ne 0) { throw "rclone copy failed (check FTP host/creds/FTP_ROOT; try FTP_TLS=true for SmarterASP)." }

    Write-Host "Done. Open a listed car's detail page on Railway to confirm its photos load." -ForegroundColor Green
}
finally {
    Remove-Item -Recurse -Force $work -ErrorAction SilentlyContinue
}
