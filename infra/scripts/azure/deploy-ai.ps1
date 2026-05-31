# Usage: pwsh deploy-ai.ps1 [-KeyVaultName <name>] [-AiAccountName <name>] [-AiLocation <region>]
#
# Deploys the Azure AI Services account (GPT-4o) and grants the backend
# managed identity the Cognitive Services OpenAI User role.
# Outputs the endpoint URL to use in values.yaml / appsettings.
#
# GPT-4o GlobalStandard quota must be available in AiLocation.
# Common options with quota: eastus, eastus2, swedencentral, westus3

param(
    [string]$KeyVaultName  = $env:KV_NAME,
    [string]$AiAccountName = 'longevity-ai',
    [string]$AiLocation    = '',
    [string]$DeploymentSku = 'GlobalStandard',
    [int]   $CapacityK     = 10
)

$ErrorActionPreference = 'Stop'
$ScriptsDir = Resolve-Path "$PSScriptRoot/.."
$InfraDir   = Resolve-Path "$ScriptsDir/.."
$Config     = & "$ScriptsDir/lib/get-config.ps1" -KeyVaultName $KeyVaultName

$SubscriptionId = $Config.subscriptionId
$RgName         = $Config.rgName
$RgLocation     = $Config.rgLocation
$ResourceLocation = if ($AiLocation) { $AiLocation } else { $RgLocation }

Write-Host "==> Registering Microsoft.CognitiveServices provider..." `
    -ForegroundColor Cyan
az provider register `
    --namespace Microsoft.CognitiveServices `
    --subscription $SubscriptionId `
    --wait `
    --only-show-errors | Out-Null

Write-Host "==> Getting backend managed identity principal ID..." `
    -ForegroundColor Cyan
$BackendPrincipalId = az identity show `
    --name 'photo-api-identity' `
    --resource-group $RgName `
    --query principalId -o tsv
if ($LASTEXITCODE -ne 0) { throw "Failed to get backend identity" }

Write-Host "==> Deploying Azure AI Services ($AiAccountName) in $ResourceLocation..." `
    -ForegroundColor Cyan

$DeploymentName = "ai-foundry-$(Get-Date -Format 'yyyyMMdd-HHmmss')"

$Deployment = az deployment sub create `
    --name $DeploymentName `
    --location $RgLocation `
    --template-file "$InfraDir/azure/ai-only.bicep" `
    --parameters rgName=$RgName `
    --parameters rgLocation=$ResourceLocation `
    --parameters aiAccountName=$AiAccountName `
    --parameters backendPrincipalId=$BackendPrincipalId `
    --parameters deploymentSku=$DeploymentSku `
    --parameters capacityK=$CapacityK `
    --subscription $SubscriptionId `
    -o json | ConvertFrom-Json
if ($LASTEXITCODE -ne 0) { throw "AI Foundry deployment failed" }

$Endpoint = $Deployment.properties.outputs.endpoint.value

Write-Host "" 
Write-Host "==> Azure AI Services deployed successfully!" -ForegroundColor Green
Write-Host "    Account : $AiAccountName"                -ForegroundColor Green
Write-Host "    Endpoint: $Endpoint"                     -ForegroundColor Green
Write-Host ""
Write-Host "Next step — add to infra/k8s/web-helm-chart/values.yaml:" `
    -ForegroundColor Yellow
Write-Host "  photoApi:"                                 -ForegroundColor Yellow
Write-Host "    aiEndpoint: `"$Endpoint`""               -ForegroundColor Yellow
