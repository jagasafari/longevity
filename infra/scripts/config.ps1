# Shared configuration — dot-source this:
#   . $PSScriptRoot/config.ps1

$ErrorActionPreference = 'Stop'

$ScriptDir = $PSScriptRoot
$InfraDir = Resolve-Path "$ScriptDir/.."
$AppDir = Resolve-Path "$InfraDir/.."

$SubscriptionId = "91b69f0b-43fb-41ca-aa83-f71f2db5ea20"
$BicepFile = "$InfraDir/azure/main.bicep"
$ParamsFile = "$InfraDir/azure/main.parameters.json"

# Read values from parameters file
$Params = Get-Content $ParamsFile | ConvertFrom-Json
$RgName = $Params.parameters.rgName.value
$RgLocation = $Params.parameters.rgLocation.value
$ClusterName = $Params.parameters.aksConfig.value.clusterName
$KeyVaultName = $Params.parameters.keyVaultName.value
$AcrName = "longevityacr"
$Namespace = "longevity"
