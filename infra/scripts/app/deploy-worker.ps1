# Usage: pwsh deploy-worker.ps1 [-Tag <string>]
param([string]$Tag)
$p = @{ Service = 'worker' }
if ($Tag) { $p.Tag = $Tag }
& $PSScriptRoot/lib/deploy-service.ps1 @p
