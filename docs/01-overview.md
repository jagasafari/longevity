# 01 — Overview

[Home](../README.md) · [Diagrams](diagrams.md) · [Services](02-services.md)

## What it does

Longevity is a personal photo-browsing application. Photos are synced from an
Android device directly to Azure Blob Storage. The app automatically generates
thumbnails, lets users organise photos into named groups, and pushes real-time
updates to the browser via SignalR.

---

## Architecture

```mermaid
graph TB
    subgraph Internet
        User[Blazor SPA<br/>in Browser]
    end

    subgraph Azure
        subgraph AKS Cluster
            ING[nginx Ingress<br/>:443 TLS]

            subgraph Frontend
                FE[nginx<br/>Blazor WASM]
            end

            subgraph Backend
                BE[F# Minimal API<br/>ASP.NET Core]
            end

            subgraph Worker
                TW[Thumbnail Worker<br/>F#]
            end

            REDIS[Redis]

            subgraph Secrets
                ESO[ExternalSecret Operator]
                TLS[TLS Secret]
                OAuth[OAuth Secret]
            end
        end

        ACR[Container Registry]
        KV[Key Vault]
        SA[Storage Account]
        EG[Event Grid]
        Q[Storage Queue]
    end

    subgraph Google
        GOAuth[Google OAuth 2.0]
    end

    User -->|HTTPS| ING
    ING -->|/| FE
    ING -->|/api, /auth, /hubs| BE
    BE -->|Token exchange| GOAuth
    BE -->|List photos + thumbnails| SA
    BE -->|Subscribe thumbnail-ready| REDIS
    BE -->|SignalR push| User
    SA -->|Blob created| EG
    EG -->|Event| Q
    Q -->|Poll| TW
    TW -->|Read photo, write thumbnail| SA
    TW -->|Publish thumbnail-ready| REDIS
    KV -->|Sync| ESO
    ESO --> TLS
    ESO --> OAuth
    OAuth -->|env vars| BE
    TLS -->|cert| ING
    ACR -->|Images| AKS

    style User fill:#4a6fa5,color:#fff
    style ING fill:#6a4a7a,color:#fff
    style FE fill:#2d8659,color:#fff
    style BE fill:#8a5a44,color:#fff
    style TW fill:#5a6a8a,color:#fff
    style REDIS fill:#a33,color:#fff
    style ACR fill:#4a6fa5,color:#fff
    style KV fill:#8a5a44,color:#fff
    style SA fill:#5a7a4a,color:#fff
    style EG fill:#4a7a6a,color:#fff
    style Q fill:#6a6a3a,color:#fff
    style GOAuth fill:#c44a3f,color:#fff
    style ESO fill:#6a6a3a,color:#fff
```

---

## Tech stack

| Layer | Technology |
|-------|------------|
| Frontend | Blazor WebAssembly (.NET 10), nginx |
| Backend API | F# / ASP.NET Core Minimal API (.NET 10) |
| Background worker | F# / .NET Generic Host |
| Database | PostgreSQL (in-cluster) — photo groups |
| Messaging | Redis Pub/Sub (in-cluster) — thumbnail notifications |
| Real-time | SignalR (`/hubs/photos`) |
| Auth | Google OAuth 2.0 + encrypted HttpOnly cookie |
| Data protection | ASP.NET Data Protection — keys persisted to Redis |
| Container images | Docker (`linux/amd64`) |
| Registry | Azure Container Registry (`longevityacr`) |
| Orchestration | AKS (Kubernetes) + Helm |
| Ingress | nginx Ingress Controller + TLS (Let's Encrypt) |
| Secrets | Azure Key Vault + ExternalSecret Operator |
| Storage | Azure Blob Storage (`longevityphotos`) |
| Events | Azure Event Grid + Storage Queue (`thumbnail-events`) |
| IaC | Bicep |
| Scripting | PowerShell 7 |

---

## Repository layout

```mermaid
graph LR
    subgraph Repository
        ROOT[longevity-app]
        WEB[src/web/]
        API[src/photo-api/]
        WORKER[src/thumbnail-worker/]
        TESTS[tests/]
        HELM[infra/k8s/web-helm-chart/]
        INFRA[infra/]
        DOCS[docs/]
    end

    ROOT --- WEB
    ROOT --- API
    ROOT --- WORKER
    ROOT --- TESTS
    ROOT --- INFRA
    ROOT --- DOCS
    INFRA --- HELM

    WEB -.- WD[Blazor WASM + nginx]
    API -.- AD[F# Minimal API]
    WORKER -.- WOD[Thumbnail worker]
    TESTS -.- TD[Unit + smoke + e2e tests]
    HELM -.- HD[Helm chart + K8s templates]
    INFRA -.- ID[Bicep + deploy scripts]
    DOCS -.- DD[Book-style documentation]

    style WEB fill:#2d8659,color:#fff
    style API fill:#8a5a44,color:#fff
    style WORKER fill:#5a6a8a,color:#fff
    style TESTS fill:#5a5a7a,color:#fff
    style HELM fill:#6a4a7a,color:#fff
    style INFRA fill:#4a6fa5,color:#fff
    style DOCS fill:#6a6a3a,color:#fff
```

```
longevity-app/
├── src/
│   ├── photo-api/          ← F# backend API      → docs/02-services.md
│   ├── thumbnail-worker/   ← F# thumbnail worker  → docs/02-services.md
│   └── web/                ← Blazor WASM frontend → docs/02-services.md
├── infra/
│   ├── azure/              ← Bicep modules        → docs/06-infrastructure.md
│   ├── k8s/                ← Helm + K8s manifests → docs/06-infrastructure.md
│   └── scripts/            ← PowerShell scripts   → docs/09-deployment.md
└── tests/
    ├── photo-api.tests/    ← Unit + smoke tests
    └── web.e2e/            ← Playwright E2E tests
```

---

## End-to-end request flow

```mermaid
sequenceDiagram
    participant SPA as Blazor SPA (Browser)
    participant ING as nginx Ingress (:443)
    participant Files as Static Files (nginx)
    participant BE as Backend (F# API)
    participant Google as Google OAuth

    Note over SPA: Single-page application running client-side

    Note over SPA,Files: Page Load

    SPA->>ING: GET / (HTTPS)
    ING->>Files: Route to frontend-svc
    Files-->>SPA: index.html + Blazor WASM

    Note over SPA,BE: API Call

    SPA->>ING: GET /api/weatherforecast
    ING->>BE: Route to backend-svc
    BE-->>SPA: JSON response

    Note over SPA,Google: Authentication
    SPA->>ING: GET /auth/login
    ING->>BE: Route to backend-svc
    BE-->>SPA: 302 -> Google consent
    SPA->>Google: Approve access
    Google-->>SPA: 302 -> /auth/callback?code=...
    SPA->>ING: GET /auth/callback?code=...
    ING->>BE: Route to backend-svc
    BE->>Google: Exchange code -> access token
    BE->>Google: Fetch email with Bearer token
    BE-->>SPA: Authorized / Denied
```

---

Next: [02 — Services](02-services.md)
