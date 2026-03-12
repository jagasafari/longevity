# Usage: pwsh deploy-app.ps1 [-Tag <string>] [-IncludeIngress]

param(
    [string]$Tag,
    [switch]$IncludeIngress
)

$ErrorActionPreference = 'Stop'
$ScriptsDir = Resolve-Path "$PSScriptRoot/.."
$InfraDir   = Resolve-Path "$ScriptsDir/.."
$Config     = Get-Content "$ScriptsDir/env.json" -Raw |
              ConvertFrom-Json

$Namespace = $Config.namespace

. $PSScriptRoot/lib/resolve-tag.ps1
$Tag = Resolve-Tag $Tag

Write-Host "`n========== BUILD + PUSH ==========" `
    -ForegroundColor Magenta

& $PSScriptRoot/lib/build-push.ps1 -Service frontend -Tag $Tag
& $PSScriptRoot/lib/build-push.ps1 -Service backend  -Tag $Tag
& $PSScriptRoot/lib/build-push.ps1 -Service worker   -Tag $Tag

if ($IncludeIngress) {
    Write-Host "`n========== INGRESS NGINX ==========" `
        -ForegroundColor Magenta
    helm upgrade --install ingress-nginx `
        ingress-nginx/ingress-nginx `
        --version $Config.ingressNginxChartVersion `
        --namespace ingress-nginx `
        -f "$InfraDir/k8s/ingress-nginx/values.yaml" `
        --wait
    if ($LASTEXITCODE -ne 0) {
        throw "Ingress NGINX upgrade failed"
    }
}

Write-Host "`n========== HELM DEPLOY ==========" `
    -ForegroundColor Magenta
helm upgrade --install web-app `
    "$InfraDir/k8s/web-helm-chart" `
    --namespace $Namespace `
    --create-namespace `
    --set "frontend.image.tag=$Tag" `
    --set "backend.image.tag=$Tag" `
    --set "worker.image.tag=$Tag"
if ($LASTEXITCODE -ne 0) { throw "Helm deployment failed" }

Write-Host "`n==> Deployed (tag: $Tag)" -ForegroundColor Green
