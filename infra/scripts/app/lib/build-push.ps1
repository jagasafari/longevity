# Usage: pwsh build-push.ps1 -Service frontend [-Tag x]
#        pwsh build-push.ps1 -Service backend  [-Tag x]

param(
    [Parameter(Mandatory)]
    [ValidateSet('frontend', 'backend', 'worker')]
    [string]$Service,
    [string]$Tag
)

$ErrorActionPreference = 'Stop'
$ScriptsDir = Resolve-Path "$PSScriptRoot/../.."
$InfraDir   = Resolve-Path "$ScriptsDir/.."
$RepoDir    = Resolve-Path "$InfraDir/.."
$Config     = Get-Content "$ScriptsDir/env.json" -Raw |
              ConvertFrom-Json

$AcrName  = $Config.acrName
$ImageName = if ($Service -eq 'worker') { 'thumbnail-worker' }
             else { "longevity-$Service" }
$AcrImage = "$AcrName.azurecr.io/$ImageName"
$Full     = "${AcrImage}:${Tag}"
$SrcDir   = if ($Service -eq 'worker') { "$RepoDir/src/thumbnail-worker" }
            else { "$RepoDir/src/longevity-$Service" }

Write-Host "==> Logging in to ACR..." -ForegroundColor Cyan
az acr login --name $AcrName
if ($LASTEXITCODE -ne 0) { throw "ACR login failed" }

Write-Host "==> Building $Service image (tag: $Tag)..." `
    -ForegroundColor Cyan
docker build --platform linux/amd64 -t $Full $SrcDir
if ($LASTEXITCODE -ne 0) { throw "$Service build failed" }

Write-Host "==> Pushing $Service image..." -ForegroundColor Cyan
docker push $Full
if ($LASTEXITCODE -ne 0) { throw "$Service push failed" }

Write-Host "==> $Service image pushed: $Full" `
    -ForegroundColor Green
