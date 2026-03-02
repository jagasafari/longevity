# Usage: pwsh setup-cluster.ps1

. $PSScriptRoot/config.ps1

Write-Host "==> Getting AKS credentials..." -ForegroundColor Cyan
az aks get-credentials `
    --resource-group $RgName `
    --name $ClusterName `
    --overwrite-existing

if ($LASTEXITCODE -ne 0) { throw "Failed to get AKS credentials" }

# --- External Secrets Operator ---
Write-Host "==> Installing External Secrets Operator..." -ForegroundColor Cyan
helm repo add external-secrets https://charts.external-secrets.io
helm repo update

helm upgrade --install external-secrets external-secrets/external-secrets `
    --namespace external-secrets `
    --create-namespace `
    --set installCRDs=true `
    --wait

if ($LASTEXITCODE -ne 0) { throw "ESO installation failed" }

Write-Host "==> Applying ClusterSecretStore..." -ForegroundColor Cyan
kubectl apply -f "$InfraDir/k8s/cluster-secret-store.yaml"
kubectl wait clustersecretstore/azure-keyvault `
    --for=condition=Ready `
    --timeout=180s

if ($LASTEXITCODE -ne 0) { throw "ClusterSecretStore not ready" }

Write-Host "==> Applying Container Insights log filter config..." -ForegroundColor Cyan
kubectl apply -f "$InfraDir/k8s/container-insights-agentconfig.yaml"
if ($LASTEXITCODE -ne 0) { throw "Container Insights agent config apply failed" }

# --- Ingress NGINX ---
Write-Host "==> Installing NGINX Ingress Controller..." -ForegroundColor Cyan
helm repo add ingress-nginx https://kubernetes.github.io/ingress-nginx
helm repo update

helm upgrade --install ingress-nginx ingress-nginx/ingress-nginx `
    --namespace ingress-nginx `
    --create-namespace `
    -f "$InfraDir/k8s/ingress-nginx-values.yaml" `
    --wait

if ($LASTEXITCODE -ne 0) { throw "Ingress NGINX installation failed" }
Write-Host "==> Cluster services configured successfully" -ForegroundColor Green
