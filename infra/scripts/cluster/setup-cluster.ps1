# Usage: pwsh setup-cluster.ps1

$ErrorActionPreference = 'Stop'
$ScriptsDir = Resolve-Path "$PSScriptRoot/.."
$InfraDir   = Resolve-Path "$ScriptsDir/.."
$Config     = Get-Content "$ScriptsDir/env.json" -Raw |
              ConvertFrom-Json

function Assert-NotPlaceholder {
    param([string]$Name, [string]$Value)
    $pattern = '^(REPLACE_ME|CHANGE_ME|TODO|YOUR_|<)'
    if ([string]::IsNullOrWhiteSpace($Value) -or
        $Value -match $pattern) {
        throw "Missing '$Name' in env.json"
    }
}

$RgName          = $Config.rgName
$ClusterName     = $Config.clusterName
$Namespace       = $Config.namespace
$DnsLabel        = $Config.dnsLabel
$IngressHostname = $Config.ingressHostname
$CertEmail       = $Config.certEmail

Assert-NotPlaceholder 'dnsLabel' $DnsLabel
Assert-NotPlaceholder 'ingressHostname' $IngressHostname
Assert-NotPlaceholder 'certEmail' $CertEmail

Write-Host "==> Getting AKS credentials..." -ForegroundColor Cyan
az aks get-credentials `
    --resource-group $RgName `
    --name $ClusterName `
    --overwrite-existing

if ($LASTEXITCODE -ne 0) { throw "Failed to get AKS credentials" }

# --- External Secrets Operator ---
Write-Host "==> Installing External Secrets Operator..." -ForegroundColor Cyan
helm repo add external-secrets https://charts.external-secrets.io
helm repo update

helm upgrade --install external-secrets external-secrets/external-secrets `
    --namespace external-secrets `
    --create-namespace `
    --set installCRDs=true `
    --wait

if ($LASTEXITCODE -ne 0) { throw "ESO installation failed" }

Write-Host "==> Applying ClusterSecretStore..." -ForegroundColor Cyan
kubectl apply -f "$InfraDir/k8s/external-secrets/cluster-secret-store.yaml"
kubectl wait clustersecretstore/azure-keyvault `
    --for=condition=Ready `
    --timeout=180s

if ($LASTEXITCODE -ne 0) { throw "ClusterSecretStore not ready" }

Write-Host "==> Applying Container Insights log filter config..." -ForegroundColor Cyan
kubectl apply -f "$InfraDir/k8s/monitoring/container-insights-agentconfig.yaml"
if ($LASTEXITCODE -ne 0) { throw "Container Insights agent config apply failed" }

# --- Ingress NGINX ---
Write-Host "==> Installing NGINX Ingress Controller..." -ForegroundColor Cyan
helm repo add ingress-nginx https://kubernetes.github.io/ingress-nginx
helm repo update

helm upgrade --install ingress-nginx ingress-nginx/ingress-nginx `
    --version $Config.ingressNginxChartVersion `
    --namespace ingress-nginx `
    --create-namespace `
    -f "$InfraDir/k8s/ingress-nginx/values.yaml" `
    --wait

if ($LASTEXITCODE -ne 0) { throw "Ingress NGINX installation failed" }

# --- Assign DNS label to ingress public IP ---
Write-Host "==> Assigning DNS label to ingress public IP..." -ForegroundColor Cyan
$ingressIp = $null
$maxIngressIpAttempts = 30
$ingressIpPollDelaySeconds = 10

for (
    $attempt = 1;
    $attempt -le $maxIngressIpAttempts -and -not $ingressIp;
    $attempt++
) {
    $ingressIp = kubectl get svc ingress-nginx-controller -n ingress-nginx `
        -o jsonpath='{.status.loadBalancer.ingress[0].ip}'

    if ($ingressIp) {
        break
    }

    Write-Host (
        "Ingress load balancer IP not assigned yet " +
        "(attempt $attempt of $maxIngressIpAttempts), " +
        "waiting $ingressIpPollDelaySeconds seconds..."
    ) -ForegroundColor Yellow

    Start-Sleep -Seconds $ingressIpPollDelaySeconds
}

if (-not $ingressIp) {
    $timeoutSeconds = $maxIngressIpAttempts * $ingressIpPollDelaySeconds
    throw (
        "Ingress load balancer IP not assigned after $timeoutSeconds " +
        "seconds - is ingress-nginx running and is the LoadBalancer " +
        "provisioning healthy?"
    )
}

$mcRg = az aks show --resource-group $RgName --name $ClusterName `
    --query nodeResourceGroup -o tsv

$ipName = az network public-ip list --resource-group $mcRg `
    --query "[?ipAddress=='$ingressIp'].name" -o tsv
if (-not $ipName) { throw "Could not find Azure public IP resource for $ingressIp" }

az network public-ip update `
    --name $ipName `
    --resource-group $mcRg `
    --dns-name $DnsLabel | Out-Null

$fqdn = az network public-ip show `
    --name $ipName `
    --resource-group $mcRg `
    --query dnsSettings.fqdn -o tsv

Write-Host "==> Ingress hostname: $fqdn" -ForegroundColor Green

if ($fqdn -ne $IngressHostname) {
    throw (
        "Computed FQDN '$fqdn' does not match expected " +
        "IngressHostname '$IngressHostname'. Update " +
        "'ingressHostname' in env.json."
    )
}

# --- cert-manager ---
Write-Host "==> Installing cert-manager..." -ForegroundColor Cyan
helm repo add jetstack https://charts.jetstack.io
helm repo update

helm upgrade --install cert-manager jetstack/cert-manager `
    --namespace cert-manager `
    --create-namespace `
    --set crds.enabled=true `
    --wait

if ($LASTEXITCODE -ne 0) { throw "cert-manager installation failed" }

# --- Let's Encrypt ClusterIssuer ---
Write-Host "==> Applying Let's Encrypt ClusterIssuer..." -ForegroundColor Cyan
$issuerYaml = Get-Content "$InfraDir/k8s/cert-manager/cluster-issuer.yaml" -Raw
$issuerYaml = $issuerYaml -replace 'CERT_EMAIL_PLACEHOLDER', $CertEmail
$issuerYaml | kubectl apply -f -

if ($LASTEXITCODE -ne 0) { throw "ClusterIssuer apply failed" }

kubectl wait clusterissuer/letsencrypt-prod `
    --for=condition=Ready `
    --timeout=60s

if ($LASTEXITCODE -ne 0) { throw "ClusterIssuer not ready" }
Write-Host "==> Cluster services configured successfully" -ForegroundColor Green
