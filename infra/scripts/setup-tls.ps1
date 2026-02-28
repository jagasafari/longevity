# Usage: pwsh setup-tls.ps1 [-Force]

param(
    [switch]$Force
)

. $PSScriptRoot/config.ps1

Write-Host "==> Checking TLS certificate in Key Vault..." -ForegroundColor Cyan
$CertExists = az keyvault secret show `
    --vault-name $KeyVaultName `
    --name web-tls-cert `
    --query id -o tsv 2>$null

if ($CertExists -and -not $Force) {
    Write-Host "==> TLS certificate already exists in Key Vault, skipping (use -Force to regenerate)" -ForegroundColor Yellow
    return
}

$CertDir = Join-Path ([System.IO.Path]::GetTempPath()) "tls-certs"
New-Item -ItemType Directory -Path $CertDir -Force | Out-Null

Write-Host "==> Generating self-signed TLS certificate..." -ForegroundColor Cyan
openssl req -x509 -newkey rsa:4096 `
    -keyout "$CertDir/tls.key" `
    -out "$CertDir/tls.crt" `
    -days 365 -nodes `
    -subj "/CN=web-ingress.local/O=longevity/C=US"

if ($LASTEXITCODE -ne 0) { throw "Certificate generation failed" }

Write-Host "==> Uploading certificate to Key Vault..." -ForegroundColor Cyan
az keyvault secret set --vault-name $KeyVaultName --name web-tls-cert --file "$CertDir/tls.crt" | Out-Null
az keyvault secret set --vault-name $KeyVaultName --name web-tls-key --file "$CertDir/tls.key" | Out-Null

if ($LASTEXITCODE -ne 0) { throw "Certificate upload failed" }
Write-Host "==> TLS certificate uploaded to Key Vault" -ForegroundColor Green
