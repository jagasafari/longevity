<#
Usage examples:
    pwsh ./scripts/rotate-sas.ps1
    pwsh ./scripts/rotate-sas.ps1 -ExpiryDays 7 -Permissions cw
    pwsh ./scripts/rotate-sas.ps1 -StorageAccountName longevityphotos -ContainerName photos -KeyVaultName longevity-kv-001 -SecretName photos-upload-sas
#>

param(
    [Parameter(Mandatory = $false)]
    [string]$StorageAccountName = "longevityphotos",

    [Parameter(Mandatory = $false)]
    [string]$ContainerName = "photos",

    [Parameter(Mandatory = $false)]
    [string]$KeyVaultName = "longevity-kv-001",

    [Parameter(Mandatory = $false)]
    [string]$SecretName = "photos-upload-sas",

    [Parameter(Mandatory = $false)]
    [int]$ExpiryDays = 7,

    [Parameter(Mandatory = $false)]
    [string]$Permissions = "cw"
)

$ErrorActionPreference = "Stop"

Write-Host "Generating new SAS token..." -ForegroundColor Cyan
$expiry = (Get-Date).ToUniversalTime().AddDays($ExpiryDays).ToString("yyyy-MM-ddTHH:mmZ")

$sas = az storage container generate-sas `
    --account-name $StorageAccountName `
    --name $ContainerName `
    --permissions $Permissions `
    --expiry $expiry `
    --auth-mode login `
    --as-user `
    -o tsv

if (-not $sas) {
    throw "Failed to generate SAS token."
}

$sas = $sas.Trim()
if ($sas.StartsWith("?")) { $sas = $sas.Substring(1) }

Write-Host "Saving SAS token to Key Vault secret '$SecretName'..." -ForegroundColor Cyan
az keyvault secret set `
    --vault-name $KeyVaultName `
    --name $SecretName `
    --value $sas | Out-Null

Write-Host "Rotation complete." -ForegroundColor Green
Write-Host "Expires (UTC): $expiry"
Write-Host "Secret: $KeyVaultName/$SecretName"

Write-Host ""
Write-Host "Run this to read SAS from Key Vault:" -ForegroundColor Yellow
Write-Host "az keyvault secret show --vault-name $KeyVaultName --name $SecretName --query value -o tsv"

Write-Host ""
Write-Host "Run this to upload a file with curl (replace /path/to/photo.jpg):" -ForegroundColor Yellow
Write-Host "SAS=`$(az keyvault secret show --vault-name $KeyVaultName --name $SecretName --query value -o tsv)"
Write-Host "curl -X PUT -H 'x-ms-blob-type: BlockBlob' -H 'Content-Type: image/jpeg' --data-binary @/path/to/photo.jpg 'https://$StorageAccountName.blob.core.windows.net/$ContainerName/photo.jpg?`$SAS'"
