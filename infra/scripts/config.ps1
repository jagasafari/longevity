# Shared configuration — dot-source this:
#   . $PSScriptRoot/config.ps1

$ErrorActionPreference = 'Stop'

$ScriptDir = $PSScriptRoot
$InfraDir = Resolve-Path "$ScriptDir/.."
$AppDir = Resolve-Path "$InfraDir/.."

$SubscriptionId = "91b69f0b-43fb-41ca-aa83-f71f2db5ea20"
$BicepFile = "$InfraDir/azure/main.bicep"
$ParamsFile = "$InfraDir/azure/main.parameters.json"
$RuntimeParamsFile = "$ScriptDir/runtime.parameters.json"

# Read values from parameters file
$Params = Get-Content $ParamsFile -Raw | ConvertFrom-Json
$RuntimeParams = if (Test-Path $RuntimeParamsFile) {
	Get-Content $RuntimeParamsFile -Raw | ConvertFrom-Json
}
else {
	[PSCustomObject]@{}
}

function Get-RequiredConfigValue {
	param(
		[Parameter(Mandatory)]
		[string]$Name,
		[Parameter(Mandatory)]
		[string]$ParamName
	)

	$value = [string]$RuntimeParams.$ParamName
	$placeholderPattern = '^(REPLACE_ME|CHANGE_ME|TODO|YOUR_|<)'

	if ([string]::IsNullOrWhiteSpace($value) -or $value -match $placeholderPattern) {
		throw (
			"Missing required config '$Name'. " +
			"Set '$ParamName' in $RuntimeParamsFile."
		)
	}

	return $value
}

$RgName = $Params.parameters.rgName.value
$RgLocation = $Params.parameters.rgLocation.value
$ClusterName = $Params.parameters.aksConfig.value.clusterName
$KeyVaultName = $Params.parameters.keyVaultName.value
$AcrName = "longevityacr"
$Namespace = "longevity"

