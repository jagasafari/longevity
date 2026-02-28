# Usage: pwsh deploy-app.ps1 [-Tag <string>]

param([string]$Tag)

$p = if ($Tag) { @{ Tag = $Tag } } else { @{} }

& $PSScriptRoot/lib/deploy-service.ps1 `
    -Service frontend @p
& $PSScriptRoot/lib/deploy-service.ps1 `
    -Service backend @p
