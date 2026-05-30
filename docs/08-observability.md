# 08 — Observability

[Docs Home](README.md) · [Workload Identity](07-workload-identity.md) · [Deployment](09-deployment.md)

**Source:** [infra/scripts/azure/check-observability.ps1](../infra/scripts/azure/check-observability.ps1) ·
[infra/azure/modules/log-analytics.bicep](../infra/azure/modules/log-analytics.bicep) ·
[infra/azure/modules/app-insights.bicep](../infra/azure/modules/app-insights.bicep) ·
[infra/azure/modules/workbook.bicep](../infra/azure/modules/workbook.bicep)

---

## Quick health check

If the workspace has been idle, start here:

```powershell
pwsh infra/scripts/azure/check-observability.ps1
```

The script resolves the Azure Monitor workbook, Log Analytics workspace, and
Application Insights resource from the live deployment. It prints direct portal
URLs and runs log-based health checks.

### What the script checks

| Check | Log source | Condition |
|-------|-----------|-----------|
| Pod health | `KubePodInventory` | Any pods not in Running / Succeeded state |
| Kube warning events | `KubeEvents` | Warning-level events in the cluster |
| App error logs | `ContainerLogV2` | Error-pattern lines in `photo-api` and `thumbnail-worker` |
| Ingress 5xx responses | nginx ingress logs | HTTP 5xx from the ingress controller |
| WAF detections | ModSecurity logs | Any ModSecurity block/detect entries |
| Storage failures | Storage diagnostic logs | Blob / queue operation failures |

### Options

```powershell
# Look back 7 days instead of the default
pwsh infra/scripts/azure/check-observability.ps1 -LookbackHours 168

# Open the Azure Portal dashboards directly in the browser
pwsh infra/scripts/azure/check-observability.ps1 -OpenPortal

# JSON output — useful for agents or CI
pwsh infra/scripts/azure/check-observability.ps1 -AsJson
```

### Exit codes

| Code | Meaning |
|------|---------|
| `0` | Healthy |
| `1` | Warning |
| `2` | Critical |

Can be used in CI or as structured input to an agent.

---

## Resources deployed

| Resource | Name | Purpose |
|----------|------|---------|
| Log Analytics workspace | `longevity-workspace` | Central log store for AKS, App Insights |
| Application Insights | `longevity-appinsights` | APM for the backend API |
| Azure Monitor workbook | `Longevity Workbook` | Pre-built dashboard with curated queries |
| Container Insights agent | Helm-deployed | Collects `KubePodInventory`, `ContainerLogV2`, `KubeEvents` |

Config: [infra/azure/modules/log-analytics.bicep](../infra/azure/modules/log-analytics.bicep) ·
[infra/azure/modules/app-insights.bicep](../infra/azure/modules/app-insights.bicep)

---

## Useful portal URLs

The script prints the exact portal links for the current deployment. You can
also navigate manually:

- **Log Analytics workspace** → Azure Portal → `longevity-workspace` → Logs
- **Application Insights** → Azure Portal → `longevity-appinsights` → Live Metrics / Failures
- **Azure Monitor workbook** → Azure Portal → Monitor → Workbooks → `Longevity Workbook`

---

## Workbook

The workbook is defined as YAML and compiled by a Python builder script.

| File | Purpose |
|------|---------|
| [infra/azure/workbook/workbook.yaml](../infra/azure/workbook/workbook.yaml) | Workbook definition (YAML) |
| [infra/azure/workbook/queries/](../infra/azure/workbook/queries/) | Individual KQL query files |
| [infra/azure/workbook/builder.py](../infra/azure/workbook/builder.py) | Compiles YAML + queries → ARM JSON |
| [infra/scripts/azure/deploy-workbook.ps1](../infra/scripts/azure/deploy-workbook.ps1) | Deploys compiled workbook to Azure |

---

## Container Insights agent config

[infra/k8s/monitoring/container-insights-agentconfig.yaml](../infra/k8s/monitoring/container-insights-agentconfig.yaml)
controls which namespaces and log streams are collected by the Container
Insights agent running in the cluster.

---

Next: [09 — Deployment](09-deployment.md)
