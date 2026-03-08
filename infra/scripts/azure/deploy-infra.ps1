# Usage: pwsh deploy-infra.ps1

$ErrorActionPreference = 'Stop'
$ScriptsDir = Resolve-Path "$PSScriptRoot/.."
$InfraDir   = Resolve-Path "$ScriptsDir/.."
$Config     = Get-Content "$ScriptsDir/env.json" -Raw |
              ConvertFrom-Json

$SubscriptionId = $Config.subscriptionId
$RgName         = $Config.rgName
$RgLocation     = $Config.rgLocation

$BicepFile              = "$InfraDir/azure/main.bicep"
$ParamsFile             = "$InfraDir/azure/main.parameters.json"
$WorkbookBuilderFile    = "$InfraDir/azure/workbook/builder.py"
$WorkbookConfigFile     = "$InfraDir/azure/workbook/workbook.yaml"
$WorkbookSerializedFile = "$InfraDir/azure/modules/workbook.serialized.json"
$WorkspaceName          = 'longevity-workspace'

function Get-ProviderState {
    param([string]$Namespace)

    az provider show `
        --namespace $Namespace `
        --subscription $SubscriptionId `
        --query registrationState `
        -o tsv
}

function Wait-ForProviderRegistration {
    param([string]$Namespace)

    $state = Get-ProviderState -Namespace $Namespace
    if ($LASTEXITCODE -ne 0) {
        throw "Failed to read provider state for $Namespace"
    }

    if ($state -eq 'Registered') {
        Write-Host "==> Provider already registered: $Namespace" `
            -ForegroundColor Green
        return
    }

    Write-Host "==> Registering provider: $Namespace" `
        -ForegroundColor Cyan
    az provider register `
        --namespace $Namespace `
        --subscription $SubscriptionId `
        --wait

    if ($LASTEXITCODE -ne 0) {
        throw "Failed to register provider $Namespace"
    }

    $maxAttempts = 30
    $attempt = 0
    $finalState = ''

    while ($attempt -lt $maxAttempts) {
        $attempt = $attempt + 1
        $finalState = Get-ProviderState `
            -Namespace $Namespace

        if ($LASTEXITCODE -ne 0) {
            throw (
                "Failed to verify provider state " +
                "for $Namespace"
            )
        }

        if ($finalState -eq 'Registered') {
            break
        }

        Start-Sleep -Seconds 5
    }

    if ($finalState -ne 'Registered') {
        throw (
            "Provider $Namespace state is " +
            "$finalState after waiting, " +
            "expected Registered"
        )
    }

    Write-Host "==> Provider registered: $Namespace" `
        -ForegroundColor Green
}

$RequiredProviders = @(
    'Microsoft.ContainerService'
    'Microsoft.OperationalInsights'
    'Microsoft.OperationsManagement'
    'Microsoft.Insights'
)

Write-Host "==> Ensuring Azure providers are registered..." `
    -ForegroundColor Cyan
$RequiredProviders | ForEach-Object {
    Wait-ForProviderRegistration -Namespace $_
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
    $Deployment = az deployment sub create `
        --name $DeploymentName `
        --location $RgLocation `
        --template-file $BicepFile `
        --parameters $ParamsFile `
        --parameters deployerPrincipalId=$DeployerPrincipalId `
        --subscription $SubscriptionId `
        -o json | ConvertFrom-Json

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
