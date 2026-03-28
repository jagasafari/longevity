# Usage: pwsh deploy-infra.ps1 [-KeyVaultName <name>]

param([string]$KeyVaultName = $env:KV_NAME)

$ErrorActionPreference = 'Stop'
$ScriptsDir = Resolve-Path "$PSScriptRoot/.."
$InfraDir   = Resolve-Path "$ScriptsDir/.."
$Config     = & "$ScriptsDir/lib/get-config.ps1" -KeyVaultName $KeyVaultName

$SubscriptionId = $Config.subscriptionId
$RgName         = $Config.rgName
$RgLocation     = $Config.rgLocation

$BicepFile              = "$InfraDir/azure/main.bicep"
$ParamsFile             = "$InfraDir/azure/main.parameters.json"
$WorkbookBuilderFile    = "$InfraDir/azure/workbook/builder.py"
$WorkbookConfigFile     = "$InfraDir/azure/workbook/workbook.yaml"
$WorkbookSerializedFile = "$InfraDir/azure/modules/workbook.serialized.json"
$WorkspaceName          = 'longevity-workspace'

Write-Host "==> Ensuring Azure providers..." `
    -ForegroundColor Cyan
foreach ($ns in $Config.requiredProviders) {
    Write-Host "  Ensuring: $ns" -ForegroundColor Cyan
    az provider register `
        --namespace $ns `
        --subscription $SubscriptionId `
        --wait `
        --only-show-errors | Out-Null
    if ($LASTEXITCODE -ne 0) {
        throw "Provider registration failed: $ns"
    }
    Write-Host "  Provider OK: $ns" -ForegroundColor Green
}

Write-Host "==> Getting deployer principal ID..." `
    -ForegroundColor Cyan
$DeployerPrincipalId = az ad signed-in-user show `
    --query id -o tsv
if ($LASTEXITCODE -ne 0) {
    throw "Failed to get deployer principal ID"
}

$DeploymentName = "longevity-$(
    Get-Date -Format 'yyyyMMdd-HHmmss'
)"

try {
    Write-Host "==> Generating workbook payload..." `
        -ForegroundColor Cyan
    $WorkspaceResourceId = (
        "/subscriptions/$SubscriptionId" +
        "/resourcegroups/$RgName" +
        "/providers/microsoft.operationalinsights" +
        "/workspaces/$WorkspaceName"
    )
    python3 $WorkbookBuilderFile `
        $WorkbookConfigFile `
        $WorkbookSerializedFile `
        $WorkspaceResourceId
    if ($LASTEXITCODE -ne 0) {
        throw "Workbook payload generation failed"
    }

    Write-Host "==> Deploying infrastructure with Bicep..." `
        -ForegroundColor Cyan

    $DeployArgs = @(
        'deployment', 'sub', 'create',
        '--name', $DeploymentName,
        '--location', $RgLocation,
        '--template-file', $BicepFile,
        '--parameters', $ParamsFile,
        '--parameters', "deployerPrincipalId=$DeployerPrincipalId",
        '--subscription', $SubscriptionId,
        '-o', 'json'
    )

    $Deployment = az @DeployArgs | ConvertFrom-Json
    if ($LASTEXITCODE -ne 0) { throw "Bicep deployment failed" }

    $WorkbookUrl =
        $Deployment.properties.outputs.workbookUrl.value

    Write-Host "==> Infrastructure deployed successfully" `
        -ForegroundColor Green

    if ($WorkbookUrl) {
        Write-Host "==> Workbook URL: $WorkbookUrl" `
            -ForegroundColor Green
    }
}
finally {
    if (Test-Path $WorkbookSerializedFile) {
        Remove-Item $WorkbookSerializedFile -Force
        Write-Host "==> Removed temporary workbook payload" `
            -ForegroundColor DarkGray
    }
}
