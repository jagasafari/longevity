# Deploy both frontend and backend
# Usage: pwsh deploy-app.ps1 [-Tag <string>]

param([string]$Tag)

$tagArgs = if ($Tag) { @('-Tag', $Tag) } else { @() }

& $PSScriptRoot/lib/deploy-service.ps1 -Service frontend @tagArgs
& $PSScriptRoot/lib/deploy-service.ps1 -Service backend  @tagArgs
