# Infrastructure

## Deploy everything

```powershell
pwsh scripts/deploy-all.ps1
```

## Deploy individually

```powershell
pwsh scripts/deploy-infra.ps1
pwsh scripts/setup-tls.ps1
pwsh scripts/setup-cluster.ps1
pwsh scripts/deploy-app.ps1
```

## Prerequisites

- Azure CLI (`az login`)
- kubectl
- Helm 3
- Docker
