#!/bin/bash
set -e

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
INFRA_DIR="$(cd "$SCRIPT_DIR/.." && pwd)"
APP_DIR="$(cd "$INFRA_DIR/.." && pwd)"

SUBSCRIPTION_ID="91b69f0b-43fb-41ca-aa83-f71f2db5ea20"
BICEP_FILE="$INFRA_DIR/bicep/main.bicep"
PARAMS_FILE="$INFRA_DIR/bicep/main.parameters.json"
FORCE_CERT_UPLOAD=false  # Set to true to force regenerate and upload TLS certificate

echo "==> Getting deploying user principal ID..."
DEPLOYER_PRINCIPAL_ID=$(az ad signed-in-user show --query id -o tsv)

echo "==> Deploying infrastructure with Bicep..."
az deployment sub create \
  --name "longevity-$(date +%Y%m%d-%H%M%S)" \
  --location westeurope \
  --template-file "$BICEP_FILE" \
  --parameters "$PARAMS_FILE" \
  --parameters deployerPrincipalId="$DEPLOYER_PRINCIPAL_ID" \
  --subscription "$SUBSCRIPTION_ID"

echo "==> Checking TLS certificate in Key Vault..."
KV_NAME=$(jq -r '.parameters.keyVaultName.value' "$PARAMS_FILE")
CERT_EXISTS=$(az keyvault secret show \
  --vault-name "$KV_NAME" \
  --name web-tls-cert \
  --query id -o tsv 2>/dev/null || echo "")

if [ -z "$CERT_EXISTS" ] || [ "$FORCE_CERT_UPLOAD" = "true" ]; then
  echo "==> Generating and uploading TLS certificate..."
  bash "$SCRIPT_DIR/generate-tls.sh" "$KV_NAME"
else
  echo "==> TLS certificate already exists in Key Vault, skipping..."
fi

echo "==> Getting AKS credentials..."
RG_NAME=$(jq -r '.parameters.rgName.value' "$PARAMS_FILE")
CLUSTER_NAME=$(jq -r '.parameters.aksConfig.value.clusterName' "$PARAMS_FILE")
az aks get-credentials \
  --resource-group "$RG_NAME" \
  --name "$CLUSTER_NAME" \
  --overwrite-existing

helm repo add external-secrets https://charts.external-secrets.io
helm repo update

helm upgrade --install external-secrets external-secrets/external-secrets \
  --namespace external-secrets \
  --create-namespace \
  --set installCRDs=true \
  --wait

echo "==> Creating ClusterSecretStore for Azure Key Vault..."
kubectl apply -f "$INFRA_DIR/k8s/cluster-secret-store.yaml"
kubectl wait clustersecretstore/azure-keyvault \
  --for=condition=Ready \
  --timeout=180s

echo "==> Adding ingress-nginx Helm repo..."
helm repo add ingress-nginx https://kubernetes.github.io/ingress-nginx
helm repo update

echo "==> Installing NGINX Ingress Controller..."
helm upgrade --install ingress-nginx ingress-nginx/ingress-nginx \
  --namespace ingress-nginx \
  --create-namespace \
  -f "$INFRA_DIR/k8s/ingress-nginx-values.yaml" \
  --wait

az acr login --name longevityacr

TAG=$(git rev-parse --short HEAD)
docker build -t longevityacr.azurecr.io/longevity-frontend:$TAG \
  /Users/mika/dev/projects/longevity/longevity-app/longevity-frontend

docker push longevityacr.azurecr.io/longevity-frontend:$TAG

echo "==> Deploying application resources with Helm..."
helm upgrade --install web-app "$APP_DIR/web-helm-chart" \
  --namespace longevity \
  --create-namespace

echo "==> Waiting for TLS secret to sync from Key Vault..."
kubectl wait externalsecret/web-tls-secret \
  -n longevity \
  --for=condition=Ready \
  --timeout=300s

echo "==> Waiting for web deployment rollout..."
kubectl rollout status deployment/web-deployment -n longevity --timeout=300s

echo "==> Deployment complete!"
echo "Get external IP with: kubectl get svc -n longevity"
