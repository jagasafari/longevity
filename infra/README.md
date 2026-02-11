# Infrastructure Deployment

## Usage

```bash
cd /Users/mika/dev/projects/longevity/longevity-app/infra
# Update the SUBSCRIPTION_ID in the script first
chmod +x deploy.sh
./deploy.sh
```

## What it does

1. Deploys AKS cluster and resource group via Bicep
2. Configures kubectl credentials
3. Installs NGINX Ingress Controller with custom values

## Prerequisites

- Azure CLI logged in (`az login`)
- kubectl installed
- Helm 3 installed
