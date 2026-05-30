# 04 — Authentication

[Home](../README.md) · [Diagrams](diagrams.md) · [API Reference](03-api-reference.md) · [Photo Pipeline](05-photo-pipeline.md)

**Source files:** [Auth.fs](../src/photo-api/Auth.fs) ·
[AuthLogin.fs](../src/photo-api/AuthLogin.fs) ·
[AuthCallback.fs](../src/photo-api/AuthCallback.fs) ·
[Routes.fs](../src/photo-api/Routes.fs)

---

## Google OAuth 2.0 flow

```mermaid
sequenceDiagram
    participant SPA as Blazor SPA (Browser)
    participant App as Backend (F# API)
    participant Google as Google OAuth

    Note over SPA: Blazor WebAssembly single-page app running client-side

    Note over SPA,Google: 1 - Login and Consent

    SPA->>App: GET /auth/login
    App-->>SPA: 302 Redirect to Google

    Note right of App: query params - client_id, redirect_uri, scope=openid email, response_type=code

    SPA->>Google: Browser follows redirect
    Google-->>SPA: Consent screen
    SPA->>Google: Approves access
    Google-->>SPA: 302 redirect to /auth/callback?code=AUTH_CODE

    Note over SPA,Google: 2 - Token Exchange (server-side)

    SPA->>App: GET /auth/callback?code=AUTH_CODE

    App->>Google: POST /token
    Note right of App: body - auth_code, client_id, client_secret, redirect_uri, grant_type

    Google-->>App: { access_token (Bearer) }
    Note right of App: access_token is short-lived (~1 hour, Google default)

    Note over SPA,Google: 3 - Session Creation

    App->>Google: GET /userinfo
    Note right of App: Authorization header uses Bearer access_token

    Google-->>App: { email }

    alt email = AllowedEmail
        App->>App: SignInAsync(ClaimsIdentity)
        Note right of App: encrypts ClaimsIdentity (AES) into cookie - cookie IS the session
        App-->>SPA: 302 Redirect / + Set-Cookie
        Note right of App: Set-Cookie .AspNetCore.Cookies=encrypted_blob, HttpOnly, SameSite=Lax
    else email does not match AllowedEmail
        App-->>SPA: 302 Redirect /?error=access_denied
        Note right of App: No cookie set, no session created
    end

    Note over SPA,Google: 4 - Session Active (every request)

    SPA->>App: GET /auth/me (Cookie auto-attached)
    App->>App: Decrypt cookie to ClaimsIdentity
    Note right of App: cookie middleware decrypts to ClaimsPrincipal in-memory - no DB lookup
    App-->>SPA: email payload then LoginDisplay shows email

    loop Every API call
        SPA->>App: GET /api/* (Cookie auto-attached)
        Note right of SPA: Browser sends cookie automatically, JS never reads or sends it
        App->>App: Decrypt cookie to ClaimsPrincipal
        alt Session valid (cookie decrypts OK)
            App-->>SPA: 200 + resource data
        else No cookie / tampered / expired
            App-->>SPA: 401 Unauthorized
            SPA->>App: GET /auth/login (new session)
        end
    end

    Note over SPA,Google: 5 - Session Destroyed (Logout)

    SPA->>App: POST /auth/logout
    App->>App: SignOutAsync expires cookie
    Note right of App: Set-Cookie with past expiry - browser deletes it, no server state
    App-->>SPA: 302 Redirect / + expired Set-Cookie
    SPA->>App: GET /auth/me returns 401 (no cookie)
    SPA->>SPA: LoginDisplay shows "Sign in" link
```

---

## Cookie-as-session model

There is **no server-side session store**. The cookie itself is the session.

```mermaid
graph LR
    subgraph "Session Lifecycle"
        A[SignInAsync] -->|serialize + encrypt| B[Cookie sent to browser]
        B -->|every request| C[Cookie middleware]
        C -->|decrypt + deserialize| D[ClaimsPrincipal]
        D --> E[HttpContext.User.Identity]
    end

    subgraph "What lives where"
        F[Browser] -->|stores| G[Encrypted cookie blob]
        H[Backend] -->|holds| I[Data Protection key]
        H -->|does NOT hold| J[Session state / user table]
    end

    style B fill:#2d8659,color:#fff
    style G fill:#2d8659,color:#fff
    style I fill:#4a6fa5,color:#fff
    style J fill:#8a5a44,color:#fff
```

| Question | Answer |
|----------|--------|
| Where is the session stored? | Inside the cookie — the browser holds the encrypted blob |
| Is there a session ID? | No — the cookie payload contains the full `ClaimsIdentity` |
| What happens on backend restart? | Sessions remain valid as long as the Data Protection key in Redis survives |
| How does it expire? | ASP.NET sets a cookie expiry date — no server-side cleanup needed |
| How does logout work? | `SignOutAsync` issues `Set-Cookie` with a past date — browser deletes it |
| Is it stateless? | Yes — any backend replica can decrypt the cookie; no sticky sessions needed |

### Data Protection key storage

Keys are persisted to Redis with the key `DataProtection-Keys`:

```fsharp
builder.Services
    .AddDataProtection()
    .SetApplicationName("longevity-app")
    .PersistKeysToStackExchangeRedis(redis, "DataProtection-Keys")
```

This means all backend replicas share the same keys and can decrypt each
other's cookies without sticky routing.

---

## Cookie security properties

```mermaid
graph TB
    subgraph "Why cookies?"
        A[SPA = public client] -->|cannot store secrets| B[Backend handles OAuth]
        B -->|issues| C[Encrypted HttpOnly cookie]
    end

    subgraph "What the cookie protects against"
        C -->|HttpOnly| D[XSS cannot read cookie via JS]
        C -->|SameSite=Lax| E[CSRF blocked on POST/PUT/DELETE]
        C -->|Encrypted| F[Content not readable or tamperable]
        C -->|Secure in prod| G[Only sent over HTTPS]
    end

    subgraph "What the cookie contains"
        C -->|decrypted by backend| H[ClaimsIdentity]
        H --> I[Email claim]
    end

    style A fill:#8a5a44,color:#fff
    style C fill:#2d8659,color:#fff
    style D fill:#4a6fa5,color:#fff
    style E fill:#4a6fa5,color:#fff
    style F fill:#4a6fa5,color:#fff
    style G fill:#4a6fa5,color:#fff
```

| Property | Value | Why |
|----------|-------|-----|
| `HttpOnly` | `true` | Invisible to JavaScript — XSS cannot steal it |
| `SameSite` | `Lax` | Browser sends cookie only to same-origin — blocks CSRF on mutations |
| Encrypted | ASP.NET Data Protection (AES + HMAC) | Cannot be forged or read |
| `Secure` | `true` in production | Only sent over HTTPS |
| No tokens in browser | — | Access token and client secret stay server-side |

---

## Credential lifecycle summary

| Phase | Credential in use |
|-------|-------------------|
| `GET /auth/login` → redirect | — |
| Google consent + callback | Authorization code (one-time, ~10 min) |
| `POST googleapis.com/token` | Code → **access token** (~1 hour) |
| `GET googleapis.com/userinfo` | **Bearer access_token** |
| `SignInAsync` | Encrypted **cookie** issued (= the session) |
| Every API request | **Cookie** (auto-attached, decrypted server-side) |
| Cookie expiry → 401 | No server cleanup |
| `POST /auth/logout` | Cookie deleted (Set-Cookie past expiry) |

---

Next: [05 — Photo Pipeline](05-photo-pipeline.md)
