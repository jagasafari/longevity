"I have a Blazor WASM frontend (web) that I want to rewrite as a React + TypeScript app. The backend is an F# ASP.NET Core API that stays unchanged.*

*Target stack: Vite + React + TypeScript strict, TanStack Query for server state, Zustand for client state, Vitest + Testing Library for tests, Zod at API boundaries.*

*The app is a photo gallery with:*
- *Paginated photo list with date filter (calendar popup) and group name filter (dropdown)*
- *Drag-and-drop to group photos together*
- *Touch drag support via a JS module (`touch-drag.js`)*
- *Group sections with hierarchical subgroups, each group can have multiple names (tags)*
- *Lightbox for full-size view*
- *Real-time updates via SignalR (`/hubs/photos`, event: `PhotosChanged`)*

*Existing API endpoints to preserve:*
- `GET /api/photos?limit=&date=&before=&groupName=`
- `GET /api/photo-groups/tree` → `{groupId, parentGroupId, photos[]}[]`
- `POST /api/photo-groups/group` `{sourceName, targetName}`
- `POST /api/photo-groups/move-to-group` `{photoName, targetGroupId}`
- `DELETE /api/photo-groups/{name}`
- `GET /api/group-names` → `string[]`
- `GET /api/group-name-assignments` → `{[groupId]: string[]}`
- `POST /api/group-names/{groupId}` `{name}`
- `DELETE /api/group-names/{groupId}/{name}`
- `GET /api/photo-counts` → `{date, count}[]`

*Start by scaffolding the project structure, Zod schemas for all API types, the Zustand store, and the TanStack Query hooks. Then implement the Home page and components."*