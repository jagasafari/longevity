# Copilot Instructions

## Self-maintenance

After solving a non-trivial task, check whether any discovered commands,
scripts, resource names, or conventions should be recorded here to avoid
re-discovering them next session. If yes, add them immediately before
finishing the response. Keep additions short and specific — one bullet
per fact. Remove bullets that become stale.

## Infrastructure

| Resource | Value |
|---|---|
| Subscription | Visual Studio Enterprise (`91b69f0b-43fb-41ca-aa83-f71f2db5ea20`) |
| Resource group | `kubernetes-resources` (location `westeurope`) |
| AKS cluster | `cluster` (node VM: `Standard_B2s`) |
| K8s namespace | `longevity` |
| ACR | `longevityacr.azurecr.io` |
| Key Vault | `longevity-kv-001` |
| Storage account | `longevityphotos` |

## Database backup

- Script: `infra/scripts/app/backup-postgres.sh` — dumps postgres to
  `backups/postgres/longevity_backup_<timestamp>.sql`
- Run before stopping pods or destructive infra changes.
- Backups are committed to the repo under `backups/postgres/`.

## Stopping / starting AKS app resources

Scale all app pods to zero (keep cluster node running):
```
kubectl -n longevity scale deployment \
  photo-api-deployment web-deployment thumbnail-worker-deployment \
  postgres-deployment redis-deployment otel-collector --replicas=0
```
Restore by re-running the deploy workflow or `helm upgrade`.

## GitHub Actions workflows

- `backend-ci.yml` — triggers on push/PR to `src/photo-api/**`
- `frontend-ci.yml` — triggers on push/PR to `src/web/**`
- `e2e.yml` — triggers on push to `main` + manual
- `deploy.yml` — triggers on push to any branch when app/chart files change
- No scheduled triggers — all workflows run only on push.

## Deployment

- Deploys run automatically via `.github/workflows/deploy.yml` on push
  to any branch when files under `src/{photo-api,web,thumbnail-worker}`
  or the Helm chart change. The workflow builds + pushes images to ACR
  and `helm upgrade`s the shared `web-app` release in namespace
  `longevity` on the single AKS cluster, then runs Playwright + the
  blob-rename e2e against the deployed env.
- Local scripts under `infra/scripts/app/deploy-*.ps1` are an
  escape-hatch for ad-hoc debugging — do not suggest them as the
  default flow.

## CI verification workflow

- After any code change, push the branch and verify all relevant
  GitHub Actions workflows succeed before declaring the task done.
- Check status with
  `gh run list -R jagasafari/longevity --branch <branch> --limit 5`.
  Filter by `headSha` to target the exact commit you just pushed.
- On failure, fetch logs with `gh run view --log-failed <run-id>`,
  fix the cause, push again, and wait for the system notification
  that the run completed. Do not poll with bare `sleep`; use the
  async terminal mode or a bounded polling loop only when necessary.
- A task is "done" only when Backend CI, Frontend CI (if touched),
  E2E, and Deploy are all green on the pushed commit.

## Voice input

The user uses voice-to-text input. English is not their native language.

When responding:
- First, silently correct any grammar, spelling, or transcription errors
- Infer correct meaning from context when words are misheard
- Before substantial technical answers, restate the interpreted request
  in one sentence and ask for confirmation
- If the user confirms, answer clearly and concisely
- If the user does not confirm yet, ask one focused clarifying question
- For voice-dictated prompts that are unclear or badly worded,
  include a brief speaking improvement note: one natural rephrasing
  and one short "next time you can say" sentence that keeps the same
  meaning
- Keep language feedback supportive, concise, and secondary to solving
  the technical request

## Code style

### General (all languages)
- Pure functional by default: no mutable state, no side effects in business logic
- Separate pure functions from impure IO (thin impure shell, thick pure core)
- Prefer expressions over statements
- Prefer immutable data structures (records, discriminated unions)
- Keep functions small, composable, and testable in isolation
- Minimize lines of code — concise > verbose
- No code comments — code should be self-documenting via clear naming
- Keep script usage comments (e.g. `# Usage: pwsh script.ps1 [-Flag]`)
- Inject side effects (randomness, IO, time) as function parameters
- Group function inputs that share the same lifecycle together
  (tuple/record) instead of passing many separate parameters
- Pass only what a function needs — never forward an entire record when a subset suffices
- Make illegal states unrepresentable: encode domain rules in types, not runtime checks
  - Use discriminated unions / sum types instead of booleans or string flags
  - Use `option`/`Result` instead of nulls or exceptions
  - Prefer narrowing inputs to the smallest valid type over defensive validation

### F#
- Keep lines to a maximum of 80 characters
- Avoid noisy pass-through wrappers
- If a function only calls another function with the same inputs and
  output, remove the wrapper and use the target function directly
- Prefer direct composition/partial application to keep code short and
  clean
- Group inputs that share the same lifecycle into tuple/record
  parameters instead of many separate arguments
- Prefer pattern matching over if/else when possible
- Keep nesting shallow: target at most 2 levels of nesting inside a
  function body; 3 only when unavoidable
- If a function starts needing nested `match`/`try`/`task` blocks,
  extract small helpers instead of nesting further
- Prefer flat pipelines over nested control flow
- Prefer partial application over inline lambdas when it improves
  readability
- Bind environment once, then pass focused functions forward
  (`let q = qs ctx`, `let run = withTransaction conn`)
- Keep impure orchestration thin:
  parse inputs -> decide via pure function -> execute IO
- Extract pure decision logic from IO code into small functions that
  return discriminated unions
- For `task` workflows, avoid mixing `try/with`, `match`, and multiple
  `let!` levels in one function when a helper can flatten it
- For database or HTTP workflows, prefer small combinators like
  `withConnection`, `withTransaction`, `parseQuery`, `requireValue`
  instead of open-coded ceremony in each handler
- Avoid deeply nested anonymous functions; give them names when reused
  or when they contain logic
- When there are multiple valid implementations, choose the one with
  the flattest control flow and clearest data flow
  - Do not produce functions with more than 3 nested scopes
  (`task`/`match`/`try`/lambda). Refactor by extracting helpers.
  - Prefer a functional shell for handlers:
  `read/parse -> pure planning function -> small execution function`
- For transactional code, prefer a reusable helper such as
  `inTransaction : IsolationLevel -> NpgsqlConnection -> (NpgsqlTransaction -> Task<'a>) -> Task<'a>`
  instead of repeating `OpenAsync` / `BeginTransactionAsync` /
  `CommitAsync` / `RollbackAsync`

### PowerShell
- Keep lines to a maximum of 94 characters
- Extract variables instead of using long inline expressions
- Group inputs that share the same lifecycle into hashtables/objects
  instead of many separate parameters
- Prefer functional style (inspired by F#/Haskell):
  - Avoid mutable variables; derive state from data
  - Use pipelines (`|`) instead of loops where possible
  - Collect results into arrays, then reduce once at the end
  - Prefer expressions over statements (e.g. `$x = if (...) { a } else { b }`)
  - No side-effectful mutation inside conditionals
