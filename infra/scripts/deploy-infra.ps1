# Deploy Azure infrastructure using Bicep
# Usage: pwsh deploy-infra.ps1

. $PSScriptRoot/config.ps1

Write-Host "==> Getting deployer principal ID..." -ForegroundColor Cyan
$DeployerPrincipalId = az ad signed-in-user show --query id -o tsv
if ($LASTEXITCODE -ne 0) { throw "Failed to get deployer principal ID" }

$DeploymentName = "longevity-$(Get-Date -Format 'yyyyMMdd-HHmmss')"

Write-Host "==> Deploying infrastructure with Bicep..." -ForegroundColor Cyan
az deployment sub create `
    --name $DeploymentName `
    --location $RgLocation `
    --template-file $BicepFile `
    --parameters $ParamsFile `
    --parameters deployerPrincipalId=$DeployerPrincipalId `
    --subscription $SubscriptionId

if ($LASTEXITCODE -ne 0) { throw "Bicep deployment failed" }
Write-Host "==> Infrastructure deployed successfully" -ForegroundColor Green
