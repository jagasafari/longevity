# Usage: pwsh deploy-workbook.ps1 [-KeyVaultName <name>]

param([string]$KeyVaultName = $env:KV_NAME)

$ErrorActionPreference = 'Stop'
$ScriptsDir = Resolve-Path "$PSScriptRoot/.."
$InfraDir   = Resolve-Path "$ScriptsDir/.."
$Config     = & "$ScriptsDir/lib/get-config.ps1" -KeyVaultName $KeyVaultName

$SubscriptionId = $Config.subscriptionId
$RgName         = $Config.rgName
$RgLocation     = $Config.rgLocation

$WorkbookBicepFile = "$InfraDir/azure/workbook-only.bicep"
$WorkbookBuilderFile = "$InfraDir/azure/workbook/builder.py"
$WorkbookConfigFile = "$InfraDir/azure/workbook/workbook.yaml"
$WorkbookSerializedFile = "$InfraDir/azure/modules/workbook.serialized.json"
$WorkbookName = 'longevity workbook'
$WorkspaceName = 'longevity-workspace'

$DeploymentName = "workbook-only-$(Get-Date -Format 'yyyyMMdd-HHmmss')"

try {
    Write-Host "==> Generating workbook payload..." -ForegroundColor Cyan
    $WorkspaceResourceId = "/subscriptions/$SubscriptionId/resourcegroups/$RgName/providers/microsoft.operationalinsights/workspaces/$WorkspaceName"
    python3 $WorkbookBuilderFile $WorkbookConfigFile $WorkbookSerializedFile $WorkspaceResourceId
    if ($LASTEXITCODE -ne 0) { throw "Workbook payload generation failed" }

    Write-Host "==> Deploying workbook only with Bicep..." -ForegroundColor Cyan
    $Deployment = az deployment sub create `
        --name $DeploymentName `
        --location $RgLocation `
        --template-file $WorkbookBicepFile `
        --parameters rgName=$RgName rgLocation=$RgLocation `
        --parameters workbookDisplayName="$WorkbookName" `
        --parameters logAnalyticsWorkspaceName="$WorkspaceName" `
        --subscription $SubscriptionId `
        -o json | ConvertFrom-Json

    if ($LASTEXITCODE -ne 0) { throw "Workbook-only Bicep deployment failed" }

    $WorkbookUrl = $Deployment.properties.outputs.workbookUrl.value

    Write-Host "==> Workbook deployed successfully" -ForegroundColor Green
    if ($WorkbookUrl) {
        Write-Host "==> Workbook URL: $WorkbookUrl" -ForegroundColor Green
    }
}
finally {
    if (Test-Path $WorkbookSerializedFile) {
        Remove-Item $WorkbookSerializedFile -Force
        Write-Host "==> Removed temporary workbook payload" -ForegroundColor DarkGray
    }
}