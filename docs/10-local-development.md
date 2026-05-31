# 10 — Local Development

[Docs Home](README.md) · [Deployment](09-deployment.md)

---

## Prerequisites

- .NET 10 SDK
- Docker (for Redis and PostgreSQL)
- Azure CLI (`az login`) — needed for Storage access via DefaultAzureCredential
- A Google OAuth 2.0 client (for auth testing)

---

## Start infrastructure dependencies

Redis and PostgreSQL are required to run the backend locally:

```bash
docker run -d --name redis   -p 6379:6379 redis:7
docker run -d --name postgres -p 5432:5432 \
  -e POSTGRES_DB=longevity \
  -e POSTGRES_USER=longevity \
  -e POSTGRES_PASSWORD=longevity \
  postgres:16
```

---

## Backend — photo-api

**Directory:** [src/photo-api](../src/photo-api)

Configuration is read from `appsettings.Development.json`. Copy and fill in
your values:

```json
{
  "GoogleOAuth": {
    "ClientId": "your-client-id",
    "ClientSecret": "your-client-secret",
    "RedirectUri": "http://localhost:5001/auth/callback",
    "AllowedEmail": "your-email@gmail.com"
  },
  "Storage": {
    "AccountName": "longevityphotos",
    "ContainerName": "photos"
  },
  "Redis": {
    "ConnectionString": "localhost:6379"
  },
  "Postgres": {
    "ConnectionString": "Host=localhost;Database=longevity;Username=longevity;Password=longevity"
  }
}
```

Run:

```bash
cd src/photo-api
dotnet run
```

The API starts on `http://localhost:5001` (or as configured in
[Properties/launchSettings.json](../src/photo-api/Properties)).

DB migrations run automatically on startup via
[DbMigrations.fs](../src/photo-api/DbMigrations.fs).

**Azure Storage access locally:** `DefaultAzureCredential` picks up your
`az login` credentials. Your Azure account needs `Storage Blob Data Reader`
and `Storage Blob Delegator` on `longevityphotos`.

---

## Frontend — web

**Directory:** [src/web](../src/web)

```bash
cd src/web
npm install
npm run dev
```

The Vite dev server is served on `http://localhost:5173` by default.
It proxies `/api`, `/auth`, and `/hubs` to the backend on
`http://localhost:5001` (see [src/web/vite.config.ts](../src/web/vite.config.ts)).

---

## Thumbnail worker

**Directory:** [src/thumbnail-worker](../src/thumbnail-worker)

Configuration (`appsettings.Development.json`):

```json
{
  "Storage": {
    "AccountName": "longevityphotos",
    "ContainerName": "photos",
    "ThumbnailContainerName": "thumbnails",
    "QueueName": "thumbnail-events"
  },
  "Redis": {
    "ConnectionString": "localhost:6379"
  }
}
```

```bash
cd src/thumbnail-worker
dotnet run
```

The worker polls the `thumbnail-events` Azure Storage Queue. For local
development the queue must exist in your Azure Storage account (it is created
by Bicep in the deployed environment).

---

## Running tests

```bash
# Unit + integration tests
dotnet test tests/photo-api.tests

# End-to-end (requires a running frontend + backend)
dotnet test tests/web.e2e
```

Test source:
[tests/photo-api.tests](../tests/photo-api.tests) ·
[tests/web.e2e](../tests/web.e2e)

---

## docker-compose (frontend only)

[src/web/docker-compose.yml](../src/web/docker-compose.yml) builds and runs
the nginx-served frontend container locally, useful for testing the production
nginx configuration.

---

[Docs Home](README.md)
