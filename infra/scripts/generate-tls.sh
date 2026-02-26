#!/bin/bash
set -e

KV_NAME="$1"
if [ -z "$KV_NAME" ]; then
  echo "Usage: $0 <key-vault-name>"
  exit 1
fi

CERT_DIR="/tmp/tls-certs"
mkdir -p "$CERT_DIR"

echo "==> Generating self-signed TLS certificate..."
openssl req -x509 -newkey rsa:4096 -keyout "$CERT_DIR/tls.key" -out "$CERT_DIR/tls.crt" \
  -days 365 -nodes \
  -subj "/CN=web-ingress.local/O=longevity/C=US"

echo "==> Uploading certificate to Key Vault..."
az keyvault secret set \
  --vault-name "$KV_NAME" \
  --name web-tls-cert \
  --file "$CERT_DIR/tls.crt"

az keyvault secret set \
  --vault-name "$KV_NAME" \
  --name web-tls-key \
  --file "$CERT_DIR/tls.key"

echo "==> Certificate uploaded to Key Vault"

rm -rf "$CERT_DIR"
