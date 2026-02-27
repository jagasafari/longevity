# Build, push Docker image and deploy app with Helm
# Usage: pwsh deploy-app.ps1 [-Tag <string>]

param(
    [string]$Tag
)

. $PSScriptRoot/config.ps1

# Resolve tag: explicit param > git SHA
if (-not $Tag) {
    $Tag = git rev-parse --short HEAD
    Write-Host "==> Using git SHA as tag: $Tag" -ForegroundColor Cyan
}

$ImageName = "$AcrName.azurecr.io/longevity-frontend"
$FullImage = "${ImageName}:${Tag}"

# --- Build & Push ---
Write-Host "==> Logging in to ACR..." -ForegroundColor Cyan
az acr login --name $AcrName
if ($LASTEXITCODE -ne 0) { throw "ACR login failed" }

Write-Host "==> Building Docker image ($FullImage)..." -ForegroundColor Cyan
docker build --platform linux/amd64 -t $FullImage "$AppDir/longevity-frontend"
if ($LASTEXITCODE -ne 0) { throw "Docker build failed" }

Write-Host "==> Pushing image to ACR..." -ForegroundColor Cyan
docker push $FullImage
if ($LASTEXITCODE -ne 0) { throw "Docker push failed" }

# --- Helm Deploy ---
Write-Host "==> Deploying application with Helm (tag: $Tag)..." -ForegroundColor Cyan
helm upgrade --install web-app "$AppDir/web-helm-chart" `
    --namespace $Namespace `
    --create-namespace `
    --set image.tag=$Tag

if ($LASTEXITCODE -ne 0) { throw "Helm deployment failed" }

Write-Host "==> Waiting for TLS secret to sync..." -ForegroundColor Cyan
kubectl wait externalsecret/web-tls-secret `
    -n $Namespace `
    --for=condition=Ready `
    --timeout=300s

Write-Host "==> Waiting for deployment rollout..." -ForegroundColor Cyan
kubectl rollout status deployment/web-deployment -n $Namespace --timeout=300s

if ($LASTEXITCODE -ne 0) { throw "Deployment rollout failed" }
Write-Host "==> Application deployed successfully (tag: $Tag)" -ForegroundColor Green
