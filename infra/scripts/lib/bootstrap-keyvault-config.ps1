# Usage: pwsh bootstrap-keyvault-config.ps1 -CertEmail <email> [-KeyVaultName <name>]
#
# One-time script: reads config from Azure + repo files and stores
# all deploy secrets in Key Vault so get-config.ps1 works without env.json.
#
# Prerequisites: az cli logged in (az login)

param(
    [string]$CertEmail,
    [string]$KeyVaultName = $env:KV_NAME
)

$ErrorActionPreference = 'Stop'

if ([string]::IsNullOrWhiteSpace($KeyVaultName)) {
    throw "Key Vault name is required. Set the KV_NAME env var or pass -KeyVaultName."
}

# --- Values from az cli ---
$SubscriptionId = az account show --query id -o tsv
if ($LASTEXITCODE -ne 0) { throw "Not logged into Azure. Run: az login" }

if ([string]::IsNullOrWhiteSpace($CertEmail)) {
    $CertEmail = kubectl get clusterissuer letsencrypt-prod `
        -o jsonpath='{.spec.acme.email}' 2>$null
    if ($LASTEXITCODE -ne 0 -or [string]::IsNullOrWhiteSpace($CertEmail)) {
        throw "Could not read cert email from cluster. Pass -CertEmail explicitly."
    }
    Write-Host "==> Cert email from cluster: $CertEmail" -ForegroundColor DarkGray
}

# --- Values baked into repo config ---
$Secrets = @{
    'deploy-subscription-id'               = $SubscriptionId
    'deploy-rg-name'                       = 'kubernetes-resources'
    'deploy-rg-location'                   = 'westeurope'
    'deploy-cluster-name'                  = 'cluster'
    'deploy-acr-name'                      = 'longevityacr'
    'deploy-namespace'                     = 'longevity'
    'deploy-dns-label'                     = 'longevity'
    'deploy-ingress-hostname'              = 'longevity.eastus2.cloudapp.azure.com'
    'deploy-cert-email'                    = $CertEmail
    'deploy-ingress-nginx-chart-version'   = '4.14.3'
}

Write-Host "`nStoring $($Secrets.Count) secrets in '$KeyVaultName'..." -ForegroundColor Cyan

foreach ($name in $Secrets.Keys) {
    $value = $Secrets[$name]
    Write-Host "  $name" -NoNewline
    az keyvault secret set `
        --vault-name $KeyVaultName `
        --name $name `
        --value $value `
        --only-show-errors | Out-Null
    if ($LASTEXITCODE -ne 0) { throw "Failed to set secret '$name'" }
    Write-Host " OK" -ForegroundColor Green
}

Write-Host "`nDone. Add to ~/.zshrc:  export KV_NAME=$KeyVaultName" -ForegroundColor Cyan
