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

