# Usage: pwsh deploy-service.ps1 -Service frontend [-Tag <string>]
#        pwsh deploy-service.ps1 -Service backend  [-Tag <string>]

param(
    [Parameter(Mandatory)]
    [ValidateSet('frontend', 'backend')]
    [string]$Service,
    [string]$Tag
)

$ErrorActionPreference = 'Stop'
$ScriptsDir = Resolve-Path "$PSScriptRoot/.."
$InfraDir   = Resolve-Path "$ScriptsDir/.."
$Config     = Get-Content "$ScriptsDir/env.json" -Raw |
              ConvertFrom-Json

$Namespace = $Config.namespace

. $PSScriptRoot/lib/resolve-tag.ps1
$Tag = Resolve-Tag $Tag

& $PSScriptRoot/lib/build-push.ps1 -Service $Service -Tag $Tag

helm upgrade --install web-app `
    "$InfraDir/k8s/web-helm-chart" `
    --namespace $Namespace `
    --create-namespace `
    --reuse-values `
    --set "$Service.image.tag=$Tag"
if ($LASTEXITCODE -ne 0) { throw "Helm deployment failed" }
