# Usage: pwsh build-push.ps1 -Service web      [-Tag x]
#        pwsh build-push.ps1 -Service photo-api [-Tag x]
#        pwsh build-push.ps1 -Service worker    [-Tag x]

param(
    [Parameter(Mandatory)]
    [ValidateSet('web', 'photo-api', 'worker')]
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
$ImageName = switch ($Service) {
    'worker'    { 'thumbnail-worker' }
    'photo-api' { 'photo-api' }
    'web'       { 'web' }
}
$AcrImage = "$AcrName.azurecr.io/$ImageName"
$Full     = "${AcrImage}:${Tag}"
$SrcDir   = switch ($Service) {
    'worker'    { "$RepoDir/src/thumbnail-worker" }
    'photo-api' { "$RepoDir/src/photo-api" }
    'web'       { "$RepoDir/src/web" }
}

Write-Host "==> Logging in to ACR..." -ForegroundColor Cyan
az acr login --name $AcrName
if ($LASTEXITCODE -ne 0) { throw "ACR login failed" }

Write-Host "==> Building $Service image (tag: $Tag)..." `
    -ForegroundColor Cyan
$BuildArgs = @('--platform', 'linux/amd64', '-t', $Full)
if ($Service -eq 'web') {
    $ConnStr = az keyvault secret show `
        --vault-name $Config.keyVaultName `
        --name 'app-insights-connection-string' `
        --query value -o tsv
    if ($LASTEXITCODE -ne 0) { throw 'Failed to fetch App Insights connection string from KeyVault' }
    $BuildArgs += '--build-arg', "APP_INSIGHTS_CONN_STR=$ConnStr"
}
docker build @BuildArgs $SrcDir
if ($LASTEXITCODE -ne 0) { throw "$Service build failed" }

Write-Host "==> Pushing $Service image..." -ForegroundColor Cyan
docker push $Full
if ($LASTEXITCODE -ne 0) { throw "$Service push failed" }

Write-Host "==> $Service image pushed: $Full" `
    -ForegroundColor Green
