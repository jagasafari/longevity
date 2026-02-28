# Usage: pwsh deploy-backend.ps1 [-Tag <string>]

param([string]$Tag)
if ($Tag) { $p.Tag = $Tag }
& $PSScriptRoot/lib/deploy-service.ps1 @p
