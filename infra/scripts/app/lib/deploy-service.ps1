# Usage: pwsh deploy-service.ps1 -Service web              [-Tag <string>]
#        pwsh deploy-service.ps1 -Service photo-api         [-Tag <string>]
#        pwsh deploy-service.ps1 -Service worker            [-Tag <string>]
#        pwsh deploy-service.ps1 -Service photo-count-worker [-Tag <string>]

param(
    [Parameter(Mandatory)]
    [ValidateSet('web', 'photo-api', 'worker', 'photo-count-worker')]
    [string]$Service,
    [string]$Tag
)

$ErrorActionPreference = 'Stop'
$ScriptsDir = Resolve-Path "$PSScriptRoot/../.."
$InfraDir   = Resolve-Path "$ScriptsDir/.."
$Config     = Get-Content "$ScriptsDir/env.json" -Raw |
              ConvertFrom-Json

$Namespace = $Config.namespace

. $PSScriptRoot/resolve-tag.ps1
$Tag = Resolve-Tag $Tag

$HelmKey = switch ($Service) {
    'photo-count-worker' { 'photoCountWorker' }
    'photo-api'          { 'photoApi' }
    default              { $Service }
}

& $PSScriptRoot/build-push.ps1 -Service $Service -Tag $Tag

helm upgrade --install web-app `
    "$InfraDir/k8s/web-helm-chart" `
    --namespace $Namespace `
    --create-namespace `
    --reset-then-reuse-values `
    --set "$HelmKey.image.tag=$Tag"
if ($LASTEXITCODE -ne 0) { throw "Helm deployment failed" }
