# TODO

## 0. Switch AI labeling to gpt-4o-mini (cost saving)

Currently `PhotoLabel.fs` uses `gpt-4o` because no `gpt-4o-mini` deployment
existed in the Azure AI Foundry resource at time of shipping.

- [x] Create the deployment:
  ```bash
  az cognitiveservices account deployment create \
    --name longevity-ai \
    --resource-group <rg> \
    --deployment-name gpt-4o-mini \
    --model-name gpt-4o-mini --model-version 2024-07-18 \
    --model-format OpenAI \
    --sku-name GlobalStandard --sku-capacity 10
  ```
- [x] In `src/photo-api/PhotoLabel.fs` change:
  ```fsharp
  let private visionModel = "gpt-4o-mini"
  let private textModel   = "gpt-4o-mini"
  ```
- [x] Deploy backend

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

