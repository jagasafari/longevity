# 07 — Workload Identity

[Docs Home](README.md) · [Diagrams](diagrams.md) · [Infrastructure](06-infrastructure.md) · [Observability](08-observability.md)

**Source:** [modules/workload-identity.bicep](../infra/azure/modules/workload-identity.bicep) ·
[modules/aks.bicep](../infra/azure/modules/aks.bicep) ·
[infra/k8s/web-helm-chart/values.yaml](../infra/k8s/web-helm-chart/values.yaml)

Both the backend and thumbnail worker access Azure Storage **without any
secrets** — no connection strings, no service principal passwords, no storage
keys. AKS Workload Identity exchanges a Kubernetes-signed JWT for an Azure
access token at runtime.

---

## Full token exchange flow

```mermaid
sequenceDiagram
    participant Kubelet as Kubelet (AKS Node)
    participant Pod as Backend Pod (F# API)
    participant OIDC as AKS OIDC Issuer Endpoint
    participant Entra as Microsoft Entra ID
    participant Blob as Azure Blob Storage

    Note over Kubelet,Entra: 1 — Pod starts: Kubelet injects a signed JWT

    Kubelet->>Kubelet: Pod spec has label, azure.workload.identity/use: "true"
    Kubelet->>Kubelet: Finds ServiceAccount backend-sa, with annotation azure.workload.identity/client-id
    Kubelet->>Pod: Mounts projected service account token, at /var/run/secrets/azure/tokens/azure-identity-token
    Note over Pod: Token is a JWT signed by AKS cluster's private key

    Note over Pod: JWT payload contains: iss: https://oidc.prod-aks.azure.com/... sub: system:serviceaccount:longevity:backend-sa aud: api://AzureADTokenExchange exp: (short-lived, auto-rotated by kubelet)

    Note over Kubelet,Entra: 2 — DefaultAzureCredential triggers token exchange

    Pod->>Pod: Code calls DefaultAzureCredential(), (Azure.Identity SDK)
    Pod->>Entra: POST /oauth2/v2.0/token, grant_type=client_credentials, client_id=<backendIdentityClientId>, client_assertion=<the mounted JWT>, client_assertion_type=urn:ietf:params:oauth:client-assertion-type:jwt-bearer, scope=https://storage.azure.com/.default

    Note over Entra: Entra ID needs to verify this JWT...

    Note over Kubelet,Entra: 3 — Entra ID validates the JWT via OIDC discovery

    Entra->>OIDC: GET /.well-known/openid-configuration
    OIDC-->>Entra: { jwks_uri: "https://oidc.prod-aks.azure.com/.../keys" }

    Entra->>OIDC: GET /keys (JWKS — public signing keys)
    OIDC-->>Entra: { keys: [ { kid: "...", n: "...", e: "..." } ] }

    Entra->>Entra: Verify JWT signature with JWKS public key (OK)
    Entra->>Entra: Check JWT not expired (OK)

    Note over Kubelet,Entra: 4 — Entra ID checks federated credential trust

    Entra->>Entra: Look up federated credentials on, managed identity longevity-backend-identity

    Note over Entra: Federated credential says: issuer = https://oidc.prod-aks.azure.com/... subject = system:serviceaccount:longevity:backend-sa audience = api://AzureADTokenExchange

    Entra->>Entra: Match JWT.iss == federated.issuer (OK)
    Entra->>Entra: Match JWT.sub == federated.subject (OK)
    Entra->>Entra: Match JWT.aud == federated.audience (OK)

    Note over Entra: All three match -> trust established

    Entra-->>Pod: Azure access token, (scoped to https://storage.azure.com), (represents longevity-backend-identity)

    Note over Pod: This Azure token carries the RBAC roles assigned to the managed identity

    Note over Pod,Blob: 5 — Backend uses Azure token to access Storage

    Pod->>Blob: GetBlobs(), Authorization: Bearer (azure-token)
    Note over Blob: RBAC check: does longevity-backend-identity have Storage Blob Data Reader? (OK)
    Blob-->>Pod: Blob list (name, lastModified, ...)

    Pod->>Pod: Sort by date, take 10

    Pod->>Blob: GetUserDelegationKey(expiry=1h), Authorization: Bearer (azure-token)
    Note over Blob: RBAC check: does identity have Storage Blob Delegator? (OK)
    Blob-->>Pod: UserDelegationKey

    Pod->>Pod: For each blob -> BlobSasBuilder, signs URL with delegation key, (read-only, 1 blob, 1 hour)

    Note over Pod,Blob: 6 — Browser loads images directly

    Pod-->>Kubelet: JSON [ { name, url, lastModified } ]
    Note over Kubelet: (response goes to Blazor SPA via ingress)

    Note over Blob: Browser fetches each URL with SAS query string No pod involved — direct blob download
```

---

## Trust chain

```mermaid
graph TD
    subgraph "Deploy Time (Bicep)"
        A[aks.bicep enables OIDC issuer] -->|exposes| B[OIDC endpoint with public keys]
        C[backend-identity.bicep creates UAMI<br/>longevity-backend-identity] --> D[Federated Credential]
        D -->|trusts| B
        C -->|principalId param| E0[storage.bicep]
        E0 --> E1[Role: Storage Blob Data Reader]
        E0 --> E2[Role: Storage Blob Delegator]
    end

    subgraph "Deploy Time (Helm)"
        F[backend-sa ServiceAccount] -->|annotation| G[client-id = UAMI.clientId]
        H[backend Deployment] -->|label| I[azure.workload.identity/use: true]
        H -->|spec| F
    end

    subgraph "Runtime (per request)"
        J[Kubelet injects signed JWT into pod] --> K[Pod sends JWT to Entra ID]
        K --> L{Entra checks federated credential}
        L -->|issuer + subject + audience match| M[Issues Azure access token]
        M --> N[Pod calls Blob Storage with token]
        N --> O[RBAC grants access]
    end

    B -.->|validates JWT signature| L
    D -.->|defines trust rules| L
    E1 -.->|authorizes| O
    E2 -.->|authorizes| O
    G -.->|identifies which UAMI| K

    style D fill:#2d8659,color:#fff
    style L fill:#4a6fa5,color:#fff
    style M fill:#2d8659,color:#fff
    style O fill:#2d8659,color:#fff
```

---

## Identity and RBAC wiring

```mermaid
flowchart TD
    subgraph Bicep["Bicep — IaC (deploy time)"]
        ST["Event Grid System Topic\n(SystemAssigned identity)"]
        BI["Backend Managed Identity\nlongevity-backend-identity"]
        WKI["Thumbnail Worker Identity\nlongevity-thumbnail-worker-identity"]

        ST -->|"Storage Queue Data Message Sender"| SA["Storage Account\nlongevityphotos"]
        BI -->|"Storage Blob Data Contributor"| SA
        BI -->|"Storage Blob Delegator"| SA
        WKI -->|"Storage Blob Data Contributor"| SA
        WKI -->|"Storage Queue Data Message Processor"| SA
    end

    subgraph Runtime["Runtime (per event)"]
        Topic["Event Grid Topic"] -->|"SystemAssigned identity"| WriteQ["Write to thumbnail-events queue"]
        BackendPod["Backend Pod\n(backend-sa ServiceAccount)"] -->|"Workload Identity: K8s JWT → Entra token"| SAS["Generate SAS token\n(Blob Delegator role)"]
        BackendPod -->|"same token"| ReadBlob["Read/write/delete blobs (photos + thumbnails)"]
        WorkerPod["Thumbnail Worker Pod\n(thumbnail-worker-sa ServiceAccount)"] -->|"Workload Identity: K8s JWT → Entra token"| ReadQ["Read + Delete from queue"]
        WorkerPod -->|"same token"| OrigBlob["Read from photos container"]
        WorkerPod -->|"same token"| ThumbBlob["Write to thumbnails container"]
    end
```

---

## RBAC roles

### Backend identity (`longevity-backend-identity`)

| Role | Scope | Purpose |
|------|-------|---------|
| Storage Blob Data Reader | Storage account | List and read blob content |
| Storage Blob Delegator | Storage account | Request user delegation keys for SAS signing |

### Worker identity (`longevity-thumbnail-worker-identity`)

| Role | Scope | Purpose |
|------|-------|---------|
| Storage Blob Data Contributor | Storage account | Read photos, write thumbnails |
| Storage Queue Data Message Processor | Storage account | Receive and delete queue messages |

### Event Grid SystemAssigned identity

| Role | Scope | Purpose |
|------|-------|---------|
| Storage Queue Data Message Sender | Storage account | Write to `thumbnail-events` queue |

---

## What each component contributes

| Component | Resource | Contribution |
|-----------|----------|--------------|
| `aks.bicep` | `oidcIssuerProfile: { enabled: true }` | OIDC endpoint for JWT verification |
| `aks.bicep` | `workloadIdentity: { enabled: true }` | Mutating webhook that injects tokens into pods |
| `workload-identity.bicep` | UAMI + federated credential | Azure identity + trust link |
| `storage.bicep` | Role assignments | Permissions scoped exactly to what each identity needs |
| `values.yaml` | `workloadIdentityClientId` | Passes UAMI client ID into the Helm chart |
| `backend-sa` ServiceAccount | `client-id` annotation | Tells kubelet which UAMI to project |
| Deployment label | `azure.workload.identity/use: "true"` | Triggers token injection |

---

## Security properties

| Property | Detail |
|----------|--------|
| No secrets in cluster | No connection strings, keys, or service principal passwords anywhere |
| Per-pod isolation | Only `backend-sa` and `thumbnail-worker-sa` are federated — frontend pod cannot exchange |
| Short-lived tokens | Kubelet auto-rotates the mounted JWT; Azure token has ~1h lifetime |
| Least privilege | Each identity holds exactly the roles it needs — nothing more |
| Revocable | Remove the federated credential or RBAC role → access stops for new tokens immediately |
| No proxy | Images flow directly from Blob Storage to the browser via SAS URLs |

---

Next: [08 — Observability](08-observability.md)
