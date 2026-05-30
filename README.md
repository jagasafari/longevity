<div align="right">

[GitHub](https://github.com/jagasafari/longevity) &nbsp;|&nbsp; [Links](docs/links.md)

</div>

# Longevity App

Personal photo gallery: upload to Azure Storage, auto-generate thumbnails, browse in real time via Blazor WebAssembly.

## Chapters

| Chapter | Covers | Use when you want to… |
|---------|--------|-----------------------|
| [01 - Overview](docs/01-overview.md) | Architecture, tech stack, repo map | Understand the whole system quickly |
| [02 - Services](docs/02-services.md) | Web frontend, F# API, thumbnail worker | Find where code lives |
| [03 - API Reference](docs/03-api-reference.md) | Auth, photos, photo groups, SignalR, health | Call or change an endpoint |
| [04 - Authentication](docs/04-auth.md) | Google OAuth, cookie sessions, Data Protection | Understand login/session behavior |
| [05 - Photo Pipeline](docs/05-photo-pipeline.md) | Upload, Event Grid, queue, worker, Redis, SignalR | Debug thumbnails or photo refresh |
| [06 - Infrastructure](docs/06-infrastructure.md) | Azure resources, Bicep, ingress, Helm chart | Change Azure, Kubernetes, or Helm |
| [07 - Workload Identity](docs/07-workload-identity.md) | AKS token exchange, federated credentials, RBAC | Debug Azure identity/RBAC issues |
| [08 - Observability](docs/08-observability.md) | Health script, Log Analytics, App Insights, workbook | Check production health |
| [09 - Deployment](docs/09-deployment.md) | Infra, cluster setup, app deployment | Deploy the app |
| [10 - Local Development](docs/10-local-development.md) | Local dependencies, config, running services, tests | Run it locally |
| [Mermaid Diagram Index](docs/diagrams.md) | All drawings and sequence diagrams | Browse only diagrams |
| [Table Index](docs/tables.md) | All important reference tables | Browse only tables |
| [Useful Links](docs/links.md) | GitHub and other external links | Find external links |

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
