# Usage: pwsh deploy-backend.ps1 [-Tag <string>]
param([string]$Tag)
$p = @{ Service = 'backend' }
if ($Tag) { $p.Tag = $Tag }
& $PSScriptRoot/lib/deploy-service.ps1 @p
