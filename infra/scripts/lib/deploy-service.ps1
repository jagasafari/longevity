# Usage: pwsh deploy-service.ps1 -Service frontend [-Tag x]
#        pwsh deploy-service.ps1 -Service backend  [-Tag x]

param(
    [Parameter(Mandatory)]
    [ValidateSet('frontend', 'backend')]
    [string]$Service,
    [string]$Tag
)

. $PSScriptRoot/../config.ps1

$cfg = @{
    frontend = @{
        HelmSet    = "frontend.image.tag"
        Deployment = "frontend-deployment"
    }
    backend  = @{
        HelmSet    = "backend.image.tag"
        Deployment = "backend-deployment"
    }
}[$Service]

$Tag = if ($Tag) { $Tag }
       else {
           $sha  = git rev-parse --short HEAD
           $ts   = [DateTimeOffset]::UtcNow.ToUnixTimeSeconds()
           $tag  = "$sha-$ts"
           Write-Host "==> Using tag: $tag" -ForegroundColor Cyan
           $tag
       }

$AcrImage = "$AcrName.azurecr.io/longevity-$Service"
$Full     = "${AcrImage}:${Tag}"
$SrcDir   = "$AppDir/longevity-$Service"

Write-Host "==> Logging in to ACR..." `
    -ForegroundColor Cyan
az acr login --name $AcrName
if ($LASTEXITCODE -ne 0) { throw "ACR login failed" }

Write-Host "==> Building $Service image..." `
    -ForegroundColor Cyan
docker build --platform linux/amd64 `
    -t $Full $SrcDir
if ($LASTEXITCODE -ne 0) {
    throw "$Service build failed"
}

Write-Host "==> Pushing $Service image..." `
    -ForegroundColor Cyan
docker push $Full
if ($LASTEXITCODE -ne 0) {
    throw "$Service push failed"
}

Write-Host "==> Deploying $Service (tag: $Tag)..." `
    -ForegroundColor Cyan
$helmSet = "$($cfg.HelmSet)=$Tag"
helm upgrade --install web-app `
    "$AppDir/web-helm-chart" `
    --namespace $Namespace `
    --create-namespace `
    --reuse-values `
    --set $helmSet

if ($LASTEXITCODE -ne 0) {
    throw "Helm deployment failed"
}

if ($Service -eq 'frontend') {
    Write-Host "==> Waiting for TLS secret (cert-manager)..." `
        -ForegroundColor Cyan
    $deadline = (Get-Date).AddSeconds(300)
    while ((Get-Date) -lt $deadline) {
        $exists = kubectl get secret web-tls -n $Namespace 2>$null
        if ($LASTEXITCODE -eq 0) { break }
        Start-Sleep 5
    }
    if ($LASTEXITCODE -ne 0) { throw "TLS secret web-tls not ready after 300s" }
}

Write-Host "==> Waiting for $Service rollout..." `
    -ForegroundColor Cyan
kubectl rollout status `
    "deployment/$($cfg.Deployment)" `
    -n $Namespace --timeout=300s
if ($LASTEXITCODE -ne 0) {
    throw "$Service rollout failed"
}

Write-Host "==> $Service deployed (tag: $Tag)" `
    -ForegroundColor Green
