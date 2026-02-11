#!/bin/bash
set -e

SUBSCRIPTION_ID="<your-subscription-id>"
RG_NAME="kubernetes-resources"
CLUSTER_NAME="cluster"
BICEP_FILE="./bicep/main.bicep"
PARAMS_FILE="./bicep/main.parameters.json"

echo "==> Deploying infrastructure with Bicep..."
az deployment sub create \
  --name "longevity-$(date +%Y%m%d-%H%M%S)" \
  --location eastus2 \
  --template-file "$BICEP_FILE" \
  --parameters "$PARAMS_FILE" \
  --subscription "$SUBSCRIPTION_ID"

echo "==> Getting AKS credentials..."
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

echo "Get external IP with: kubectl get svc -n ingress-nginx"
