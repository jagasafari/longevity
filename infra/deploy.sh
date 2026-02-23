#!/bin/bash
set -e

SUBSCRIPTION_ID="91b69f0b-43fb-41ca-aa83-f71f2db5ea20"
BICEP_FILE="./bicep/main.bicep"
PARAMS_FILE="./bicep/main.parameters.json"
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
  --subscription "$SUBSCRIPTION_ID" \
  --debug

echo "==> Checking TLS certificate in Key Vault..."
KV_NAME=$(jq -r '.parameters.keyVaultName.value' "$PARAMS_FILE")
CERT_EXISTS=$(az keyvault secret show \
  --vault-name "$KV_NAME" \
  --name web-tls-cert \
  --query id -o tsv 2>/dev/null || echo "")

if [ -z "$CERT_EXISTS" ] || [ "$FORCE_CERT_UPLOAD" = "true" ]; then
  echo "==> Generating and uploading TLS certificate..."
  bash ./generate-tls.sh
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
  --wait

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
