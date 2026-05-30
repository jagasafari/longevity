# 02 — Services

[Home](../README.md) · [Diagrams](diagrams.md) · [Overview](01-overview.md) · [API Reference](03-api-reference.md)

---

## Frontend — React + TypeScript SPA

**Source:** [src/web](../src/web)

A Vite-built React + TypeScript single-page application served as static
files by nginx. All logic runs client-side; the backend is hit only for
data and auth.

### Stack

- Vite 6 + React 19 + TypeScript (strict)
- TanStack Query v5 (server state, infinite pagination)
- Zustand (UI state)
- Zod (runtime validation at API boundaries)
- Tailwind CSS v4 (CSS-first `@theme` config)
- `@microsoft/signalr` for the `/hubs/photos` real-time channel
- Vitest + Testing Library

### Pages & components

| File | Purpose |
|------|---------|
| [src/web/src/pages/Home.tsx](../src/web/src/pages/Home.tsx) | Photo gallery with group tree, filters, calendar |
| [src/web/src/components/Layout.tsx](../src/web/src/components/Layout.tsx) | Page shell with sign-in / sign-out bar |
| [src/web/src/components/PhotoCard.tsx](../src/web/src/components/PhotoCard.tsx) | Single photo tile with thumbnail + drag handlers |
| [src/web/src/components/GroupSection.tsx](../src/web/src/components/GroupSection.tsx) | Recursive group section (renders subgroups + photos) |
| [src/web/src/components/GroupHeader.tsx](../src/web/src/components/GroupHeader.tsx) | Group title + category chips + assign UI |
| [src/web/src/components/CalendarPopup.tsx](../src/web/src/components/CalendarPopup.tsx) | Date-picker for filtering photos by day |
| [src/web/src/components/Lightbox.tsx](../src/web/src/components/Lightbox.tsx) | Full-size view |
| [src/web/src/api/client.ts](../src/web/src/api/client.ts) | Typed fetch client + Zod parsing |
| [src/web/src/api/hooks.ts](../src/web/src/api/hooks.ts) | TanStack Query hooks for every endpoint |
| [src/web/src/api/signalr.ts](../src/web/src/api/signalr.ts) | SignalR `PhotosChanged` subscription |

### nginx configuration

nginx serves the static Vite build output and applies a `try_files`
catch-all so unknown paths return `index.html` (required for SPA
client-side routing). See [src/web/docker](../src/web/docker).

### Service topology

```mermaid
graph LR
   subgraph Browser
      SPA[React SPA]
   end

   subgraph Frontend Pod
      Nginx[nginx :80 - Static Files SPA]
   end

   subgraph Backend Pod
      API[F# API :8080]
   end

   SPA -->|index.html + JS bundle| Nginx
   SPA -->|GET /api/*| API
   SPA -->|GET /auth/*| API
   SPA -->|WebSocket /hubs/photos| API

   style SPA fill:#4a6fa5,color:#fff
   style Nginx fill:#2d8659,color:#fff
   style API fill:#8a5a44,color:#fff
```

### Request flow

```mermaid
sequenceDiagram
    participant SPA as React SPA (Browser)
    participant Ingress as nginx Ingress
    participant Files as Static Files (nginx)
    participant BE as Backend Pod (F# API)

    Note over SPA: Single-page application running client-side

    Note over SPA,Files: Initial Page Load

    SPA->>Ingress: GET /
    Ingress->>Files: Route to frontend-svc
    Files-->>SPA: index.html + Vite JS/CSS bundle
    Note right of Files: ~110 KB gzip first load
    SPA->>SPA: React hydrates, TanStack Query boots

    Note over SPA,BE: Data fetch
    SPA->>Ingress: GET /api/photos?limit=...
    Ingress->>BE: Route to backend-svc
    BE-->>SPA: JSON page (validated client-side via Zod)

    Note over SPA,BE: Deep Link / Refresh
    SPA->>Ingress: GET /some/path (browser refresh)
    Ingress->>Files: Route to frontend-svc
    Files-->>SPA: index.html (try_files fallback)
```

### Authentication in the SPA

```mermaid
sequenceDiagram
   participant User
   participant SPA as React SPA (Browser)
   participant BE as Backend (F# API)

   Note over User,BE: Check session on page load
   SPA->>BE: GET /auth/me (cookie auto-attached)
   alt Cookie present
      BE-->>SPA: { email }
      SPA->>User: Shows email + "Sign out"
   else No cookie
      BE-->>SPA: 401
      SPA->>User: Shows "Sign in with Google"
   end

   Note over User,BE: Sign in
   User->>SPA: Clicks "Sign in with Google"
   SPA->>BE: Browser navigates to /auth/login
   BE-->>SPA: 302 -> Google -> consent -> callback
   BE-->>SPA: 302 Redirect / + Set-Cookie
   SPA->>BE: GET /auth/me (new cookie)
   BE-->>SPA: { email }

   Note over User,BE: Sign out
   User->>SPA: Clicks "Sign out"
   SPA->>BE: POST /auth/logout
   BE-->>SPA: 302 Redirect / + expired cookie
```

---

## Backend — photo-api (F# ASP.NET Core)

**Source:** [src/photo-api](../src/photo-api)  
**Port (in-cluster):** 8080  
**Framework:** ASP.NET Core Minimal API, .NET 10

### Key source files

| File | Responsibility |
|------|----------------|
| [Program.fs](../src/photo-api/Program.fs) | DI wiring, middleware pipeline, route registration |
| [Routes.fs](../src/photo-api/Routes.fs) | Pure handler functions for every endpoint |
| [Auth.fs](../src/photo-api/Auth.fs) | OAuth result types and email allow-list check |
| [AuthLogin.fs](../src/photo-api/AuthLogin.fs) | Build Google OAuth redirect URL |
| [AuthCallback.fs](../src/photo-api/AuthCallback.fs) | Exchange auth code for email via Google token endpoint |
| [Storage.fs](../src/photo-api/Storage.fs) | List blobs, generate User Delegation SAS URLs, delete blobs |
| [PhotoGroups.fs](../src/photo-api/PhotoGroups.fs) | PostgreSQL CRUD for photo groups (pure planning + impure IO) |
| [GroupNames.fs](../src/photo-api/GroupNames.fs) | Resolve all photo names belonging to a named group |
| [PhotoHub.fs](../src/photo-api/PhotoHub.fs) | SignalR hub — broadcasts `PhotosChanged` events |
| [ThumbnailSubscriber.fs](../src/photo-api/ThumbnailSubscriber.fs) | Redis subscriber: listens on `thumbnail-ready`, notifies hub |
| [PhotoCountCache.fs](../src/photo-api/PhotoCountCache.fs) | In-memory cache of photo counts, refreshed in background |
| [DbMigrations.fs](../src/photo-api/DbMigrations.fs) | Runs PostgreSQL schema migrations on startup |
| [Config.fs](../src/photo-api/Config.fs) | Typed config loading for OAuth, Storage, PostgreSQL |

### Services registered

- **SignalR** — hub at `/hubs/photos`
- **Redis** (`StackExchange.Redis`) — for pub/sub and Data Protection key storage
- **ASP.NET Data Protection** — keys persisted to Redis key `DataProtection-Keys`
- **`ThumbnailSubscriberService`** — hosted service subscribing to Redis `thumbnail-ready`
- **`PhotoCountCache` + `RefreshService`** — background cache for photo counts
- **HttpClient factory** — for Google OAuth HTTP calls

### Startup sequence

1. Load config (OAuth, Storage, Postgres, Redis)
2. Connect to Redis (`ConnectionMultiplexer`)
3. Register Data Protection with Redis backing
4. Build and start the app
5. Run DB migrations (`DbMigrations.run`)
6. Register routes

---

## Thumbnail Worker — F#

**Source:** [src/thumbnail-worker](../src/thumbnail-worker)  
**Framework:** .NET Generic Host (background service)

### What it does

1. On startup, runs a **catch-up scan**: generates thumbnails for any photos
   that already exist in Blob Storage but have no corresponding thumbnail.
2. Enters a **poll loop**: receives up to 10 messages at a time from the
   Azure Storage Queue `thumbnail-events`. For each message:
   - Parses the blob name from the Event Grid JSON payload.
   - Downloads the original photo from the `photos` container.
   - Resizes it to 400 px width using **ImageSharp**.
   - Uploads the JPEG to the `thumbnails` container.
   - Deletes the queue message (acknowledgement).
   - Publishes `thumbnail-ready` to Redis so the backend can notify connected browsers.
3. If the queue is empty, backs off for 5 seconds before polling again.

### Key source files

| File | Responsibility |
|------|----------------|
| [Program.fs](../src/thumbnail-worker/Program.fs) | Host setup, config loading, DI |
| [ThumbnailProcessor.fs](../src/thumbnail-worker/ThumbnailProcessor.fs) | Pure image resize logic, blob I/O, queue parsing |

### Authentication

Uses **AKS Workload Identity** — no secrets in the container image or
environment. See [07 — Workload Identity](07-workload-identity.md) for the
full token-exchange flow.

---

Next: [03 — API Reference](03-api-reference.md)
