# Usage:
#   pwsh backfill-thumbnail-cache-control.ps1                 # dry-run
#   pwsh backfill-thumbnail-cache-control.ps1 -Apply
#   pwsh backfill-thumbnail-cache-control.ps1 -Apply -StorageAccount longevityphotos
#
# Sets Cache-Control: public, max-age=31536000, immutable on every blob in the
# thumbnails container that does not already have it. Safe to re-run.

[CmdletBinding()]
param(
    [switch]$Apply,
    [string]$StorageAccount = 'longevityphotos',
    [string]$Container = 'thumbnails',
    [string]$CacheControl = 'public, max-age=31536000, immutable'
)

$ErrorActionPreference = 'Stop'

$blobs = az storage blob list `
    --account-name $StorageAccount `
    --container-name $Container `
    --auth-mode login `
    --query "[].{name:name, cc:properties.cacheControl, ct:properties.contentSettings.contentType}" `
    -o json | ConvertFrom-Json

if (-not $blobs) {
    Write-Host "No blobs found in $Container"
    return
}

$needsUpdate = $blobs | Where-Object { $_.cc -ne $CacheControl }
Write-Host "Total blobs: $($blobs.Count)"
Write-Host "Needs update: $($needsUpdate.Count)"

if (-not $Apply) {
    Write-Host "Dry-run. Pass -Apply to perform updates."
    return
}

$i = 0
foreach ($b in $needsUpdate) {
    $i++
    Write-Host "[$i/$($needsUpdate.Count)] $($b.name)"
    az storage blob update `
        --account-name $StorageAccount `
        --container-name $Container `
        --name $b.name `
        --auth-mode login `
        --content-cache $CacheControl `
        --content-type 'image/jpeg' `
        -o none
}

Write-Host "Done."
