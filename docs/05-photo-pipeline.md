# 05 — Photo Pipeline

[Home](../README.md) · [Diagrams](diagrams.md) · [Authentication](04-auth.md) · [Infrastructure](06-infrastructure.md)

**Source files:**
[src/thumbnail-worker/ThumbnailProcessor.fs](../src/thumbnail-worker/ThumbnailProcessor.fs) ·
[src/photo-api/ThumbnailSubscriber.fs](../src/photo-api/ThumbnailSubscriber.fs) ·
[src/photo-api/Storage.fs](../src/photo-api/Storage.fs) ·
[src/photo-api/PhotoHub.fs](../src/photo-api/PhotoHub.fs)

---

## Full event flow

```mermaid
sequenceDiagram
    participant Phone as Android App
    participant Blob as Azure Blob (photos)
    participant EG as Event Grid
    participant Q as Storage Queue
    participant TW as Thumbnail Worker
    participant Thumb as Azure Blob (thumbnails)
    participant Redis as Redis
    participant BE as Backend
    participant SPA as Blazor SPA

    Note over Phone,Blob: Upload

    Phone->>Blob: Sync photo from DCIM

    Note over Blob,TW: Thumbnail Generation

    Blob->>EG: BlobCreated event
    EG->>Q: Enqueue event
    TW->>Q: Poll queue
    Q-->>TW: Event message
    TW->>Blob: Download photo
    Blob-->>TW: Photo bytes
    TW->>Thumb: Upload resized thumbnail

    Note over TW,SPA: Real-time Notification & Fetch

    TW->>Redis: PUBLISH thumbnail-ready
    Redis->>BE: Notify subscriber
    BE->>SPA: SignalR "PhotosChanged"

    Note over SPA,Thumb: Delegated SAS Fetch

    SPA->>BE: GET /api/photos (authenticated)
    BE->>BE: GetUserDelegationKeyAsync (Entra ID)
    BE->>BE: Build SAS URL per blob, (BlobSasPermissions.Read, 1h TTL)
    BE-->>SPA: PhotoInfo[] with thumbnailUrl = SAS URL
    SPA->>Thumb: GET thumbnailUrl (direct, no backend proxy)
    Thumb-->>SPA: JPEG bytes
```

## Azure Storage event routing

```mermaid
sequenceDiagram
    participant Phone as Android App
    participant Blob as Blob Storage (photos container)
    participant Thumbs as Blob Storage (thumbnails container)
    participant Queue as Storage Queue (thumbnail-events)
    participant Topic as System Topic (storage events)
    participant Worker as Thumbnail Worker (K8s pod)

    Note over Phone,Queue: 1 — Photo upload

    Phone->>Blob: HTTP PUT /photos/{name}, (SAS token, direct upload)
    Blob-->>Blob: Blob written

    Note over Blob,Queue: 2 — Event Grid captures BlobCreated and routes it

    Blob->>Topic: BlobCreated event emitted, (built-in, no config needed)
    Topic->>Topic: Filter: container = photos, type = BlobCreated
    Topic->>Queue: Write JSON message, (authenticates with SystemAssigned identity)

    Note over Queue,Thumbs: 3 — Worker processes the event

    Worker->>Queue: ReceiveMessages (long-poll, 5s backoff when empty)
    Queue-->>Worker: Message: { subject: "/blobs/photos/{name}", ... }
    Worker->>Worker: Parse blob name from subject
    Worker->>Blob: Download original photo, (Workload Identity -> Managed Identity token)
    Worker->>Worker: Resize to 400px width (ImageSharp)
    Worker->>Thumbs: Upload resized JPEG, (same Managed Identity)
    Worker->>Queue: DeleteMessage (acknowledge)
```

---

## Worker startup — catch-up scan

On startup the worker runs a catch-up scan before entering the poll loop:

```
1. List all blobs in the photos container
2. For each photo with no matching thumbnail in the thumbnails container:
   a. Download the photo
   b. Resize and upload the thumbnail
3. If any thumbnails were generated, publish "thumbnail-ready catch-up" to Redis
```

This ensures photos uploaded while the worker was offline get thumbnails
without needing to re-trigger Event Grid.

---

## Delegated SAS fetch

No storage account keys are used. The backend authenticates with its Managed
Identity and issues short-lived **User Delegation SAS** tokens.

```mermaid
sequenceDiagram
    participant SPA as Blazor SPA
    participant App as Backend (F# API)
    participant AAD as Entra ID
    participant Blob as Blob Storage

    Note over SPA,Blob: 1 — Authenticated request for photos

    SPA->>App: GET /api/photos (Cookie)
    App->>App: RequireAuthorization() validates cookie

    Note over SPA,Blob: 2 — Managed Identity authenticates to Storage

    App->>AAD: DefaultAzureCredential()
    Note right of App: AKS workload identity or local Azure CLI credentials
    AAD-->>App: OAuth2 token (Storage scope)

    Note over SPA,Blob: 3 — List blobs + generate User Delegation SAS

    App->>Blob: GetBlobs() with Bearer token
    Note right of App: Requires role: Storage Blob Data Reader
    Blob-->>App: Blob list (name, lastModified, ...)

    App->>App: Sort by lastModified desc, take 10

    App->>Blob: GetUserDelegationKey(expiry=1h)
    Note right of App: Requires role: Storage Blob Delegator
    Blob-->>App: UserDelegationKey (signed by Entra ID)

    App->>App: For each blob: BlobSasBuilder, + ToSasQueryParameters(delegationKey)
    Note right of App: Produces read-only URL per blob valid for 1 hour, scoped to that blob

    Note over SPA,Blob: 4 — SPA loads images directly from Blob Storage

    App-->>SPA: JSON array of { name, url, lastModified }
    Note right of App: Each url contains ?sv=...&sig=... SAS token

    loop For each photo
        SPA->>Blob: GET blob URL with SAS token
        Note right of SPA: img src= triggers browser fetch no cookie or backend involved
        Blob-->>SPA: Image bytes
    end
```

### Account key SAS vs User Delegation SAS

```mermaid
graph TB
    subgraph "Account Key SAS (not used)"
        A1[Storage account key] -->|signs| A2[SAS token]
        A1 -->|if leaked| A3[Full account access]
        style A1 fill:#8a5a44,color:#fff
        style A3 fill:#8a5a44,color:#fff
    end

    subgraph "User Delegation SAS (used)"
        B1[Managed Identity] -->|authenticates via| B2[Entra ID]
        B2 -->|issues| B3[UserDelegationKey]
        B3 -->|signs| B4[SAS token]
        B4 -->|scoped| B5[Read-only, 1 blob, 1 hour]
        style B1 fill:#2d8659,color:#fff
        style B4 fill:#2d8659,color:#fff
        style B5 fill:#4a6fa5,color:#fff
    end
```

| Property | Value |
|----------|-------|
| No secrets in config | Only `Storage:AccountName` — no keys or connection strings |
| Least privilege | SAS grants read-only access to a single blob |
| Short-lived | SAS expires after 1 hour |
| Revocable | Removing the managed identity's RBAC stops new SAS tokens immediately |
| No proxy | Images are served directly from Blob Storage to the browser |

---

## Identity summary

Three separate identities are involved in the pipeline — none use keys.

| Identity | Used by | Roles |
|----------|---------|-------|
| Event Grid SystemAssigned | Event Grid → Queue | `Storage Queue Data Message Sender` |
| `longevity-backend-identity` | Backend pod | `Blob Data Contributor`, `Blob Delegator` |
| `longevity-thumbnail-worker-identity` | Worker pod | `Blob Data Contributor`, `Queue Data Message Processor` |

Bicep modules: [modules/photo-events.bicep](../infra/azure/modules/photo-events.bicep) ·
[modules/storage.bicep](../infra/azure/modules/storage.bicep) ·
[modules/workload-identity.bicep](../infra/azure/modules/workload-identity.bicep)

---

Next: [06 — Infrastructure](06-infrastructure.md)
