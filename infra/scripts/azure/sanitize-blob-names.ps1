# Usage:
#   pwsh sanitize-blob-names.ps1                   # dry-run
#   pwsh sanitize-blob-names.ps1 -Apply            # perform renames
#   pwsh sanitize-blob-names.ps1 -Apply -StorageAccount longevityphotos
#
# Renames every blob in the photos and thumbnails containers whose name
# contains characters outside [A-Za-z0-9._-], and updates matching rows
# in photo_group_members.photo_name and vocabulary.photos.photo_name.
#
# Per blob: copy -> verify -> UPDATE rows in a transaction -> delete source.
# Idempotent (re-running after a partial failure is safe).

[CmdletBinding()]
param(
    [switch]$Apply,
    [string]$StorageAccount = 'longevityphotos',
    [string[]]$Containers = @('photos', 'thumbnails'),
    [string]$Namespace = 'longevity',
    [string]$DbUser = 'longevity',
    [string]$DbName = 'longevity',
    [string]$SecretName = 'postgres-credentials'
)

$ErrorActionPreference = 'Stop'

function Sanitize([string]$name) {
    $replaced = [regex]::Replace($name, '[^A-Za-z0-9._\-]+', '-')
    $trimmed = $replaced.Trim('-', '.')
    if ([string]::IsNullOrEmpty($trimmed)) { 'file' } else { $trimmed }
}

function Get-PortForwardPid {
    param([string]$PodName)
    $job = Start-Process kubectl `
        -ArgumentList @('port-forward', '-n', $Namespace,
                        "pod/$PodName", '5433:5432') `
        -PassThru `
        -RedirectStandardOutput /tmp/pf-out.log `
        -RedirectStandardError /tmp/pf-err.log
    Start-Sleep -Seconds 3
    $job.Id
}

function Stop-PortForward {
    param([int]$ProcessId)
    if ($ProcessId) {
        Stop-Process -Id $ProcessId -Force -ErrorAction SilentlyContinue
    }
}

function Invoke-Sql {
    param([string]$Sql, [string]$Password)
    $env:PGPASSWORD = $Password
    & psql -h localhost -p 5433 -U $DbUser -d $DbName `
        -v ON_ERROR_STOP=1 -At -c $Sql
    if ($LASTEXITCODE -ne 0) { throw "psql failed: $Sql" }
}

function List-Blobs([string]$Container) {
    $json = az storage blob list `
        --account-name $StorageAccount `
        --container-name $Container `
        --auth-mode login `
        --query "[].name" -o json
    if ($LASTEXITCODE -ne 0) { throw "az storage blob list failed for $Container" }
    $json | ConvertFrom-Json
}

function Copy-Blob([string]$Container, [string]$From, [string]$To) {
    $accountUrl = "https://$StorageAccount.blob.core.windows.net"
    az storage blob copy start `
        --account-name $StorageAccount `
        --auth-mode login `
        --destination-container $Container `
        --destination-blob $To `
        --source-uri "$accountUrl/$Container/$([System.Uri]::EscapeDataString($From))" `
        --requires-sync true | Out-Null
    if ($LASTEXITCODE -ne 0) {
        throw "Copy failed: $Container/$From -> $To"
    }
}

function Blob-Exists([string]$Container, [string]$Name) {
    $exists = az storage blob exists `
        --account-name $StorageAccount `
        --container-name $Container `
        --auth-mode login `
        --name $Name `
        --query exists -o tsv
    return ($exists -eq 'true')
}

function Delete-Blob([string]$Container, [string]$Name) {
    az storage blob delete `
        --account-name $StorageAccount `
        --container-name $Container `
        --auth-mode login `
        --delete-snapshots include `
        --name $Name | Out-Null
}

# ---------- main ----------

Write-Host "Mode: $(if ($Apply) { 'APPLY' } else { 'DRY-RUN' })" -ForegroundColor Yellow
Write-Host "Storage account: $StorageAccount"
Write-Host ""

# Compute rename plan per container
$plans = @{}
foreach ($container in $Containers) {
    Write-Host "Scanning $container ..." -ForegroundColor Cyan
    $names = List-Blobs $container
    $renames = @()
    $existing = [System.Collections.Generic.HashSet[string]]::new(
        [string[]]$names, [System.StringComparer]::Ordinal)
    foreach ($n in $names) {
        $s = Sanitize $n
        if ($s -ne $n) {
            $collision = $existing.Contains($s)
            $renames += [PSCustomObject]@{
                From = $n; To = $s; Collision = $collision
            }
        }
    }
    $plans[$container] = $renames
    Write-Host "  $($renames.Count) blobs need renaming ($(($renames | Where-Object Collision).Count) collisions)" -ForegroundColor Cyan
    foreach ($r in $renames) {
        $mark = if ($r.Collision) { '[SKIP-COLLISION]' } else { '[OK]' }
        Write-Host "  $mark $($r.From) -> $($r.To)"
    }
    Write-Host ""
}

$totalActionable = ($plans.Values | ForEach-Object {
    $_ | Where-Object { -not $_.Collision }
}).Count

if ($totalActionable -eq 0) {
    Write-Host "Nothing to do." -ForegroundColor Green
    exit 0
}

if (-not $Apply) {
    Write-Host "Dry-run complete. Re-run with -Apply to perform $totalActionable rename(s)." `
        -ForegroundColor Yellow
    exit 0
}

# Find postgres pod
$pod = kubectl get pods -n $Namespace -o name |
    Select-String -Pattern 'postgres-deployment' |
    Select-Object -First 1
if (-not $pod) { throw "No postgres pod found in namespace $Namespace" }
$podName = ($pod.Line -split '/')[1]
Write-Host "Postgres pod: $podName"

$password = kubectl get secret $SecretName -n $Namespace `
    -o jsonpath='{.data.password}' | base64 -d
if (-not $password) { throw "Could not read $SecretName" }

$pfPid = Get-PortForwardPid -PodName $podName
try {
    foreach ($container in $Containers) {
        foreach ($r in ($plans[$container] | Where-Object { -not $_.Collision })) {
            Write-Host "[$container] $($r.From) -> $($r.To)" -ForegroundColor Green
            Copy-Blob $container $r.From $r.To
            if (-not (Blob-Exists $container $r.To)) {
                Write-Warning "Copy verify failed for $($r.To); skipping delete + DB update"
                continue
            }
            # Only update DB rows for the source container (photos), not thumbnails
            if ($container -eq 'photos') {
                $fromEsc = $r.From.Replace("'", "''")
                $toEsc = $r.To.Replace("'", "''")
                $sql = @"
BEGIN;
UPDATE photo_group_members SET photo_name = '$toEsc' WHERE photo_name = '$fromEsc';
UPDATE vocabulary.photos    SET photo_name = '$toEsc' WHERE photo_name = '$fromEsc';
COMMIT;
"@
                Invoke-Sql -Sql $sql -Password $password
            }
            Delete-Blob $container $r.From
        }
    }
    Write-Host ""
    Write-Host "Done." -ForegroundColor Green
}
finally {
    Stop-PortForward -ProcessId $pfPid
}
