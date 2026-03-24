param([string]$BaseUrl, [switch]$IncludeAuthChecks)
$ErrorActionPreference = "Stop"
. $PSScriptRoot/lib/test-helpers.ps1

if (!$BaseUrl) {
    Write-Host "Missing -BaseUrl" -ForegroundColor Red
    Write-Host "  pwsh ./smoke-test-local.ps1 -BaseUrl http://localhost:5001"
    exit 1
}

$BaseUrl = $BaseUrl.TrimEnd('/')
$http    = New-TestHttpClient

Write-Host "`n==> Smoke test: $BaseUrl`n" `
    -ForegroundColor Cyan

$results = @(
    Test-Endpoint $http "GET /healthz" "$BaseUrl/healthz"
)

if ($IncludeAuthChecks) {
    $results += Test-Endpoint $http `
        "GET /auth/login" `
        "$BaseUrl/auth/login" `
        -Expected 200,302,401,404
    $results += Test-Endpoint $http `
        "GET /auth/callback" `
        "$BaseUrl/auth/callback" `
        -Expected 200,302,400,401,404
}

Write-TestResult $results
