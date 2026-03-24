# Usage: pwsh deploy-photo-count-worker.ps1 [-Tag <string>]
param([string]$Tag)
$p = @{ Service = 'photo-count-worker' }
if ($Tag) { $p.Tag = $Tag }
& $PSScriptRoot/lib/deploy-service.ps1 @p
