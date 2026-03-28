# Usage: $Config = & "$ScriptsDir/lib/get-config.ps1" [-KeyVaultName <name>]
#
# Fetches deploy config from Azure Key Vault.
# Requires the KV_NAME environment variable, or pass -KeyVaultName explicitly.
#
# One-time setup — store each value with:
#   az keyvault secret set --vault-name <vault> --name deploy-subscription-id  --value <subscriptionId>
#   az keyvault secret set --vault-name <vault> --name deploy-rg-name           --value <rgName>
#   az keyvault secret set --vault-name <vault> --name deploy-rg-location       --value <rgLocation>
#   az keyvault secret set --vault-name <vault> --name deploy-cluster-name      --value <clusterName>
#   az keyvault secret set --vault-name <vault> --name deploy-acr-name          --value <acrName>
#   az keyvault secret set --vault-name <vault> --name deploy-namespace         --value <namespace>
#   az keyvault secret set --vault-name <vault> --name deploy-dns-label         --value <dnsLabel>
#   az keyvault secret set --vault-name <vault> --name deploy-ingress-hostname  --value <ingressHostname>
#   az keyvault secret set --vault-name <vault> --name deploy-cert-email        --value <certEmail>
#   az keyvault secret set --vault-name <vault> --name deploy-ingress-nginx-chart-version --value <version>
#
# Then add to your shell profile (~/.zshrc or $PROFILE):
#   export KV_NAME=<your-vault-name>

param([string]$KeyVaultName = $env:KV_NAME)

if ([string]::IsNullOrWhiteSpace($KeyVaultName)) {
    throw "Key Vault name is required. Set the KV_NAME environment variable or pass -KeyVaultName."
}

function Get-Secret {
    param([string]$Name, [switch]$Optional)
    $val = az keyvault secret show `
        --vault-name $KeyVaultName `
        --name $Name `
        --query value -o tsv 2>$null
    if ($LASTEXITCODE -ne 0) {
        if ($Optional) { return $null }
        throw "Secret '$Name' not found in vault '$KeyVaultName'. Run the one-time setup in get-config.ps1."
    }
    $val
}

$rawProviders = Get-Secret 'deploy-required-providers' -Optional

[PSCustomObject]@{
    subscriptionId           = Get-Secret 'deploy-subscription-id'
    rgName                   = Get-Secret 'deploy-rg-name'
    rgLocation               = Get-Secret 'deploy-rg-location'
    clusterName              = Get-Secret 'deploy-cluster-name'
    acrName                  = Get-Secret 'deploy-acr-name'
    keyVaultName             = $KeyVaultName
    namespace                = Get-Secret 'deploy-namespace'
    dnsLabel                 = Get-Secret 'deploy-dns-label'
    ingressHostname          = Get-Secret 'deploy-ingress-hostname'
    certEmail                = Get-Secret 'deploy-cert-email'
    ingressNginxChartVersion = Get-Secret 'deploy-ingress-nginx-chart-version' -Optional
    requiredProviders        = if ($rawProviders) { $rawProviders | ConvertFrom-Json } else { @() }
}
