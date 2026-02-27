# Full deployment: infrastructure + cluster services + app
# Usage: pwsh deploy-all.ps1 [-SkipInfra] [-SkipCluster] [-SkipApp] [-ForceTls] [-Tag <string>]

param(
    [switch]$SkipInfra,
    [switch]$SkipCluster,
    [switch]$SkipApp,
    [switch]$ForceTls,
    [string]$Tag
)

$ErrorActionPreference = 'Stop'
$ScriptDir = $PSScriptRoot

if (-not $SkipInfra) {
    Write-Host "`n========== INFRASTRUCTURE ==========" -ForegroundColor Magenta
    & "$ScriptDir/deploy-infra.ps1"

    Write-Host "`n========== TLS CERTIFICATE ==========" -ForegroundColor Magenta
    if ($ForceTls) {
        & "$ScriptDir/setup-tls.ps1" -Force
    } else {
        & "$ScriptDir/setup-tls.ps1"
    }
} else {
    Write-Host "==> Skipping infrastructure deployment" -ForegroundColor Yellow
}

if (-not $SkipCluster) {
    Write-Host "`n========== CLUSTER SERVICES ==========" -ForegroundColor Magenta
    & "$ScriptDir/setup-cluster.ps1"
} else {
    Write-Host "==> Skipping cluster setup" -ForegroundColor Yellow
}

if (-not $SkipApp) {
    Write-Host "`n========== APPLICATION ==========" -ForegroundColor Magenta
    $appParams = @{}
    if ($Tag) { $appParams['Tag'] = $Tag }
    & "$ScriptDir/deploy-app.ps1" @appParams
} else {
    Write-Host "==> Skipping app deployment" -ForegroundColor Yellow
}

Write-Host "`n========== DEPLOYMENT COMPLETE ==========" -ForegroundColor Green
Write-Host "Get external IP: kubectl get svc -n ingress-nginx"
