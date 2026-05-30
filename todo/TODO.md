# TODO

## 1. Automated post-deploy validation

Goal: protect every change with a lightweight safety net that runs
automatically after a deployment completes.

### Phase 1 — no post-deploy errors (simple baseline)
- [ ] After `deploy-frontend.ps1` / `deploy-backend.ps1` finish, query
      Application Insights (or the AKS pod logs) for error-level entries
      in the N minutes following the rollout
- [ ] Fail the script (non-zero exit) if any new errors appear
- [ ] Add a `Invoke-PostDeployCheck` helper in `infra/scripts/lib/`
      that both deploy scripts call

### Phase 2 — re-run tests as part of deploy
- [ ] Run `dotnet test` for `photo-api.tests` before pushing the image
      (gate the build on green tests)
- [ ] Run smoke tests (`web.e2e`) against the live URL after deploy
      and surface failures

### Phase 3 — full pipeline
- [ ] Wire the above into a GitHub Actions workflow that triggers on
      merge to `main`

---

## 2. Rewrite frontend to React + TypeScript

Goal: replace Blazor WASM with React + TypeScript to eliminate scoped-CSS
footguns, reduce initial load size (~10 MB WASM → small JS bundle), and
get a better component/styling DX.

Keep the F# backend and SignalR hub unchanged.

### Tasks
- [ ] Scaffold a Vite + React + TypeScript app in `src/web-react/`
- [ ] Replace `PhotoService.cs` with typed `fetch` wrappers (one file
      per resource: `groups.ts`, `photos.ts`, `groupNames.ts`)
- [ ] Connect to the `PhotoHub` SignalR endpoint using
      `@microsoft/signalr` — subscribe to `PhotosChanged` and
      invalidate the query cache
- [ ] Port components: `PhotoCard`, `GroupTreeNode`, `RootGroupSection`,
      `CalendarPopup`, `Home` (main page)
- [ ] Move all styles from `app.css` into co-located CSS modules or
      Tailwind classes — no global style leakage
- [ ] Update `Dockerfile` and Helm chart to serve the new Vite build
      output instead of Blazor's `wwwroot`
- [ ] Delete `src/web/` once the React app is verified in production
