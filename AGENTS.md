# Longevity App — AI Agent Instructions

This file guides Codex, GitHub Copilot, Antigravity/Gemini, and other AI agents
working in this repository.

---

## What this app does

**Longevity App** is a personal photo gallery: upload photos to Azure Storage,
auto-generate thumbnails via an event-driven pipeline, and browse in real time
through a React + TypeScript SPA with F# API backend.

---

## Repository layout

| Path | What it is |
|------|-----------|
| `src/photo-api/` | F# ASP.NET Core API — auth, photos, photo groups, SignalR |
| `src/thumbnail-worker/` | F# background worker — Event Grid → queue → thumbnail generation |
| `src/web/` | React + TypeScript SPA — photo browser, upload, real-time updates |
| `infra/` | Bicep infrastructure (AKS, Azure Storage, Event Grid, Redis) |
| `docs/` | Architecture chapters and diagrams |
| `tests/` | Integration and unit tests |

---

## Tech stack

- **Frontend**: React, TypeScript, Vite
- **Backend**: F#, ASP.NET Core, SignalR
- **Worker**: F#, Azure Storage Queue
- **Auth**: Google OAuth, cookie sessions
- **Infra**: AKS (Kubernetes), Azure Blob Storage, Event Grid, Redis, Helm, Bicep
- **Region**: `swedencentral` — do not change

---

## Build & test

```bash
# F# API
cd src/photo-api
dotnet build
dotnet test

# F# Worker
cd src/thumbnail-worker
dotnet build
dotnet test

# Web
cd src/web
npm install
npm run build
npm test
```

---

## Key docs

| Doc | What it covers |
|-----|----------------|
| `docs/01-overview.md` | Architecture, tech stack, repo map |
| `docs/02-services.md` | Web frontend, F# API, thumbnail worker |
| `docs/03-api-reference.md` | Auth, photos, groups, SignalR, health |
| `docs/05-photo-pipeline.md` | Upload, Event Grid, queue, worker, Redis |
| `docs/06-infrastructure.md` | Azure resources, Bicep, ingress, Helm |
| `docs/09-deployment.md` | Deploy the app |
| `docs/10-local-development.md` | Run it locally |

---

## Key constraints

- Azure region is **swedencentral** — do not change.
- Kubernetes workload identity uses federated credentials — do not alter RBAC bindings without reading `docs/07-workload-identity.md`.
- `minSdk` does not apply (not an Android app).

## Engineering rules

- Prefer immutable F# record types and discriminated unions for domain models.
- Keep API routes as thin adapters; domain logic in pure functions.
- Use exhaustive pattern matching on DU cases.
- Test state transitions and decoders independently of HTTP/Azure dependencies.
