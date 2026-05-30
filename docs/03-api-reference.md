# 03 — API Reference

[Docs Home](README.md) · [Services](02-services.md) · [Authentication](04-auth.md)

All routes are implemented in [src/photo-api/Routes.fs](../src/photo-api/Routes.fs)
and registered in [src/photo-api/Program.fs](../src/photo-api/Program.fs).

---

## Authentication endpoints

| Method | Route | Auth required | Description |
|--------|-------|:---:|-------------|
| `GET` | `/auth/login` | — | Redirect to Google consent screen |
| `GET` | `/auth/callback` | — | Exchange OAuth code, set session cookie, redirect `/` |
| `GET` | `/auth/me` | Cookie | Returns `{ email }` or `401` |
| `POST` | `/auth/logout` | Cookie | Expire cookie, redirect `/` |

### `/auth/login`

Redirects the browser to Google's OAuth 2.0 consent URL.  
Query params sent to Google: `client_id`, `redirect_uri`, `scope=openid email`,
`response_type=code`.

### `/auth/callback?code=…`

Exchanges the authorization code for an access token, fetches the user's email,
checks it against the allow-list, and issues an encrypted HttpOnly cookie.

**Success** → `302 /`  
**Denied** → `302 /?error=access_denied`  
**Error** → `302 /?error=<message>`

### `/auth/me`

```json
{ "email": "user@example.com" }
```

Returns `401 { "error": "Not authenticated" }` if no valid session cookie.

---

## Photo endpoints

| Method | Route | Auth | Description |
|--------|-------|:----:|-------------|
| `GET` | `/api/photos` | Cookie | Paginated photo list with SAS thumbnail URLs |
| `DELETE` | `/api/photos/{name}` | Cookie | Delete a photo and remove it from all groups |

### `GET /api/photos`

Returns a page of photos sorted by `lastModified` descending.

**Query parameters:**

| Parameter | Type | Default | Description |
|-----------|------|---------|-------------|
| `limit` | int (1–200) | `50` | Page size |
| `date` | `yyyyMMdd` | — | Filter to photos taken on this date |
| `before` | ISO 8601 | — | Cursor: only photos older than this timestamp |
| `groupName` | string | — | Filter to photos belonging to this named group |

**Response:**

```json
{
  "items": [
    {
      "name": "photos/IMG_20240101_120000.jpg",
      "url": "https://longevityphotos.blob.core.windows.net/thumbnails/...?sv=...&sig=...",
      "lastModified": "2024-01-01T12:00:00Z"
    }
  ],
  "nextBefore": "2024-01-01T11:59:00Z"
}
```

Each `url` is a **User Delegation SAS URL** scoped to a single blob, valid for
1 hour. The browser loads images directly from Blob Storage — the backend is
not a proxy. See [05 — Photo Pipeline](05-photo-pipeline.md#delegated-sas-fetch)
for details.

`nextBefore` is `null` when there are no more pages.

### `DELETE /api/photos/{name}`

Deletes the blob from Blob Storage, removes the photo from all groups in
PostgreSQL, and broadcasts `PhotosChanged` via SignalR.

| Response | Meaning |
|----------|---------|
| `204 No Content` | Deleted successfully |
| `400 Bad Request` | `name` is blank |
| `404 Not Found` | Blob does not exist |
| `403 Forbidden` | RBAC denied |

---

## Photo groups endpoints

| Method | Route | Auth | Description |
|--------|-------|:----:|-------------|
| `GET` | `/api/photo-groups` | Cookie | Flat list of all groups and their photos |
| `GET` | `/api/photo-groups/tree` | Cookie | Nested tree of groups |
| `POST` | `/api/photo-groups/group` | Cookie | Group two photos together |
| `POST` | `/api/photo-groups/move-to-group` | Cookie | Move a photo to an existing group |
| `DELETE` | `/api/photo-groups/{name}` | Cookie | Remove a photo from all groups |

Photo group state is persisted in **PostgreSQL**. All mutations broadcast
`PhotosChanged` via SignalR so connected browsers update in real time.

### `POST /api/photo-groups/group`

```json
{ "sourceName": "photos/a.jpg", "targetName": "photos/b.jpg" }
```

Groups two photos together. The exact operation depends on their current group
membership (see the `GroupChange` discriminated union in
[PhotoGroups.fs](../src/photo-api/PhotoGroups.fs)):

| State | Result |
|-------|--------|
| Neither photo in a group | Creates a new group containing both |
| Source in a group, target not | Adds target to source's group |
| Target in a group, source not | Adds source to target's group |
| Both in different groups | Moves source into target's group |
| Both in the same group | Creates a subgroup |

Returns `204 No Content` on success, `400` if names are missing or identical.

### `POST /api/photo-groups/move-to-group`

```json
{ "photoName": "photos/a.jpg", "targetGroupId": "grp_abc123" }
```

Moves a photo to an existing group by its ID.

### `DELETE /api/photo-groups/{name}`

Removes the photo from all groups it belongs to and broadcasts `PhotosChanged`.

---

## Real-time (SignalR)

| Hub route | Event | Payload | Trigger |
|-----------|-------|---------|---------|
| `/hubs/photos` | `PhotosChanged` | _(none)_ | New thumbnail ready, photo deleted, group mutated |

The frontend connects to this hub on load. When `PhotosChanged` fires, the SPA
re-fetches `/api/photos` to refresh the gallery.

The backend subscribes to the Redis channel `thumbnail-ready` via
[ThumbnailSubscriber.fs](../src/photo-api/ThumbnailSubscriber.fs). When the
thumbnail worker publishes to that channel, the subscriber calls
`hub.Clients.All.SendAsync("PhotosChanged")`.

---

## Health check

| Method | Route | Auth | Description |
|--------|-------|:----:|-------------|
| `GET` | `/healthz` | — | Returns `200 OK` — used by Kubernetes liveness/readiness probes |

---

Next: [04 — Authentication](04-auth.md)
