# Usage: pwsh deploy-frontend.ps1 [-Tag <string>]
param([string]$Tag)

if ($Tag) {
    & $PSScriptRoot/lib/deploy-service.ps1 `
        -Service frontend -Tag $Tag
}
else {
    & $PSScriptRoot/lib/deploy-service.ps1 `
        -Service frontend
}
