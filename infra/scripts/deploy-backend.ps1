# Usage: pwsh deploy-backend.ps1 [-Tag <string>]
param([string]$Tag)

if ($Tag) {
    & $PSScriptRoot/lib/deploy-service.ps1 `
        -Service backend -Tag $Tag
}
else {
    & $PSScriptRoot/lib/deploy-service.ps1 `
        -Service backend
}
