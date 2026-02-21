#!/bin/bash
set -e

SUBSCRIPTION_ID="91b69f0b-43fb-41ca-aa83-f71f2db5ea20"
BICEP_FILE="./bicep/main.bicep"
PARAMS_FILE="./bicep/main.parameters.json"
echo "==> Deploying infrastructure with Bicep..."
#az deployment sub create \
az deployment sub what-if \
  --name "longevity-$(date +%Y%m%d-%H%M%S)" \
  --location westeurope \
  --template-file "$BICEP_FILE" \
  --parameters "$PARAMS_FILE" \
  --subscription "$SUBSCRIPTION_ID" \
  --verbose

echo "==> Generating and uploading self-signed TLS certificate to Key Vault..."
KV_NAME=$(jq -r '.parameters.keyVaultName.value' "$PARAMS_FILE")
bash ./generate-tls.sh

echo "==> Getting AKS credentials..."
RG_NAME=$(jq -r '.parameters.rgName.value' "$PARAMS_FILE")
CLUSTER_NAME=$(jq -r '.parameters.aksConfig.value.clusterName' "$PARAMS_FILE")
az aks get-credentials \
  --resource-group "$RG_NAME" \
  --name "$CLUSTER_NAME" \
  --overwrite-existing

echo "==> Adding ingress-nginx Helm repo..."
helm repo add ingress-nginx https://kubernetes.github.io/ingress-nginx
helm repo update

echo "==> Installing NGINX Ingress Controller..."
helm upgrade --install ingress-nginx ingress-nginx/ingress-nginx \
  --namespace ingress-nginx \
  --create-namespace \
  -f ./ingress-nginx-values.yaml \
  --wait

echo "==> Deploying application resources with Helm..."
helm upgrade --install web-app ../web-helm-chart \
  --namespace default \
  --create-namespace \
  --wait

echo "==> Deployment complete!"
echo "Get external IP with: kubectl get svc -n default"
