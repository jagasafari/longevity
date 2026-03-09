# Usage: pwsh deploy-all.ps1 [-SkipInfra] [-SkipCluster] [-SkipApp] [-Tag <string>] [-IncludeIngress]

param(
    [switch]$SkipInfra,
    [switch]$SkipCluster,
    [switch]$SkipApp,
    [string]$Tag,
    [switch]$IncludeIngress
)

$ErrorActionPreference = 'Stop'
$ScriptDir = $PSScriptRoot

if (-not $SkipInfra) {
    Write-Host "`n========== INFRASTRUCTURE ==========" -ForegroundColor Magenta
    & "$ScriptDir/azure/deploy-infra.ps1"
} else {
    Write-Host "==> Skipping infrastructure deployment" -ForegroundColor Yellow
}

if (-not $SkipCluster) {
    Write-Host "`n========== CLUSTER SERVICES ==========" -ForegroundColor Magenta
    & "$ScriptDir/cluster/setup-cluster.ps1"
} else {
    Write-Host "==> Skipping cluster setup" -ForegroundColor Yellow
}

if (-not $SkipApp) {
    Write-Host "`n========== APPLICATION ==========" -ForegroundColor Magenta
    $appParams = @{}
    if ($Tag) { $appParams['Tag'] = $Tag }
    if ($IncludeIngress) { $appParams['IncludeIngress'] = $true }
    & "$ScriptDir/app/deploy-app.ps1" @appParams
} else {
    Write-Host "==> Skipping app deployment" -ForegroundColor Yellow
}

Write-Host "`n========== DEPLOYMENT COMPLETE ==========" -ForegroundColor Green
Write-Host "Get external IP: kubectl get svc -n ingress-nginx"
