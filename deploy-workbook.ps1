[CmdletBinding()]
param(
    [Parameter(Mandatory = $false)]
    [string]$SubscriptionId = 'f1a9431d-60f7-4001-a459-8d6cca0a199a',

    [Parameter(Mandatory = $false)]
    [string]$ResourceGroupName = 'rg-sdc000000-mgmt-app-insights',

    [Parameter(Mandatory = $false)]
    [string]$AppInsightsName = 'sdc000000-mgmt-app-insights-test',

    [Parameter(Mandatory = $false)]
    [string]$TemplateFile = (Join-Path $PSScriptRoot 'workbook-deploy.bicep'),

    [Parameter(Mandatory = $false)]
    [string]$WorkbookDisplayName = 'saxo systemtest'
)

$ErrorActionPreference = 'Stop'

if (-not (Get-Module -ListAvailable -Name Az.Resources)) {
    throw 'Az.Resources module is required. Install it with: Install-Module Az -Scope CurrentUser'
}

Import-Module Az.Accounts -ErrorAction Stop
Import-Module Az.Resources -ErrorAction Stop

$context = Get-AzContext
if (-not $context -or $context.Subscription.Id -ne $SubscriptionId) {
    Connect-AzAccount -SubscriptionId $SubscriptionId -ErrorAction Stop | Out-Null
} else {
    try {
        Set-AzContext -SubscriptionId $SubscriptionId -ErrorAction Stop | Out-Null
    } catch {
        Write-Host 'Token expired or MFA required. Re-authenticating...' -ForegroundColor Yellow
        Connect-AzAccount -SubscriptionId $SubscriptionId -ErrorAction Stop | Out-Null
    }
}

$deploymentName = "workbook-$(Get-Date -Format 'yyyyMMddHHmmss')"

Write-Host "Starting deployment '$deploymentName' in resource group '$ResourceGroupName'..." -ForegroundColor Cyan

$builderScript = Join-Path $PSScriptRoot 'build-workbook-body.ps1'
if (Test-Path $builderScript) {
    & $builderScript
}

$templateToUse = $TemplateFile
$cleanupJson = $false

if ($TemplateFile -like '*.bicep') {
    $bicepInPath = Get-Command bicep -ErrorAction SilentlyContinue
    if (-not $bicepInPath) {
        Write-Host 'Bicep not in PATH. Compiling to ARM JSON...' -ForegroundColor Yellow
        $jsonTemplate = [System.IO.Path]::ChangeExtension($TemplateFile, 'json')
        az bicep build --file $TemplateFile --outfile $jsonTemplate
        if ($LASTEXITCODE -ne 0) {
            throw 'Failed to compile Bicep template.'
        }
        $templateToUse = $jsonTemplate
        $cleanupJson = $true
    }
}

$deploymentParameters = @{
    Name                         = $deploymentName
    ResourceGroupName            = $ResourceGroupName
    TemplateFile                 = $templateToUse
    workbookDisplayName          = $WorkbookDisplayName
    appInsightsSubscriptionId    = $SubscriptionId
    appInsightsResourceGroup     = $ResourceGroupName
    appInsightsName              = $AppInsightsName
}

try {
    $deployment = New-AzResourceGroupDeployment @deploymentParameters
    
    if ($deployment.ProvisioningState -ne 'Succeeded') {
        throw "Deployment failed with state: $($deployment.ProvisioningState)"
    }
} finally {
    if ($cleanupJson -and (Test-Path $templateToUse)) {
        Remove-Item $templateToUse -Force
    }
}

$workbookUrl = $deployment.Outputs.workbookPortalWorkbookUrl.value

Write-Host 'Workbook deployment succeeded.' -ForegroundColor Green
Write-Host "Workbook URL: $workbookUrl"