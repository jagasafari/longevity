# Usage: pwsh deploy-workbook.ps1

. $PSScriptRoot/config.ps1

$WorkbookBicepFile = "$InfraDir/bicep/workbook-only.bicep"
$WorkbookName = 'longevity workbook'
$WorkspaceName = 'longevity-workspace'

$DeploymentName = "workbook-only-$(Get-Date -Format 'yyyyMMdd-HHmmss')"

Write-Host "==> Deploying workbook only with Bicep..." -ForegroundColor Cyan
$Deployment = az deployment sub create `
    --name $DeploymentName `
    --location $RgLocation `
    --template-file $WorkbookBicepFile `
    --parameters rgName=$RgName rgLocation=$RgLocation workbookDisplayName="$WorkbookName" logAnalyticsWorkspaceName="$WorkspaceName" `
    --subscription $SubscriptionId `
    -o json | ConvertFrom-Json

if ($LASTEXITCODE -ne 0) { throw "Workbook-only Bicep deployment failed" }

$WorkbookUrl = $Deployment.properties.outputs.workbookUrl.value

Write-Host "==> Workbook deployed successfully" -ForegroundColor Green
if ($WorkbookUrl) {
    Write-Host "==> Workbook URL: $WorkbookUrl" -ForegroundColor Green
}