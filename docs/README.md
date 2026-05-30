# Longevity App Documentation

Longevity is a personal photo app: photos are uploaded to Azure Storage,
thumbnails are generated automatically, and a Blazor WebAssembly gallery shows
photos and groups in real time.

## Read by goal

| If you want to... | Read |
|-------------------|------|
| Understand the whole system quickly | [01 - Overview](01-overview.md) |
| Find where code lives | [02 - Services](02-services.md) |
| Call or change an endpoint | [03 - API Reference](03-api-reference.md) |
| Understand login/session behavior | [04 - Authentication](04-auth.md) |
| Debug thumbnails or photo refresh | [05 - Photo Pipeline](05-photo-pipeline.md) |
| Change Azure, Kubernetes, or Helm | [06 - Infrastructure](06-infrastructure.md) |
| Debug Azure identity/RBAC issues | [07 - Workload Identity](07-workload-identity.md) |
| Check production health | [08 - Observability](08-observability.md) |
| Deploy the app | [09 - Deployment](09-deployment.md) |
| Run it locally | [10 - Local Development](10-local-development.md) |
| Browse only diagrams | [Mermaid Diagram Index](diagrams.md) |
| Browse only tables | [Table Index](tables.md) |

## Chapters

| Chapter | Covers |
|---------|--------|
| [01 - Overview](01-overview.md) | Product purpose, architecture, tech stack, repo map |
| [02 - Services](02-services.md) | Web frontend, F# API, thumbnail worker |
| [03 - API Reference](03-api-reference.md) | Auth, photos, photo groups, SignalR, health check |
| [04 - Authentication](04-auth.md) | Google OAuth, encrypted cookie sessions, Data Protection |
| [05 - Photo Pipeline](05-photo-pipeline.md) | Upload, Event Grid, queue, worker, Redis, SignalR, SAS fetch |
| [06 - Infrastructure](06-infrastructure.md) | Azure resources, Bicep modules, ingress, Helm chart |
| [07 - Workload Identity](07-workload-identity.md) | AKS token exchange, federated credentials, RBAC |
| [08 - Observability](08-observability.md) | Health script, Log Analytics, App Insights, workbook |
| [09 - Deployment](09-deployment.md) | Infra, cluster setup, app deployment, workbook deployment |
| [10 - Local Development](10-local-development.md) | Local dependencies, config, running services, tests |
| [Mermaid Diagram Index](diagrams.md) | All drawings and sequence diagrams |
| [Table Index](tables.md) | All important reference tables |

## Project map

```
src/photo-api/          F# ASP.NET Core API
src/web/                Blazor WebAssembly frontend
src/thumbnail-worker/   F# background thumbnail worker
infra/azure/            Bicep infrastructure
infra/k8s/              Helm chart and Kubernetes manifests
infra/scripts/          PowerShell deployment scripts
tests/                  API and end-to-end tests
```
