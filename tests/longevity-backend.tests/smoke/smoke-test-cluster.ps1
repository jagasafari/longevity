param(
    [string]$BaseUrl = "https://20.69.196.2",
    [switch]$IncludeAuthChecks
)

$ErrorActionPreference = "Stop"
. $PSScriptRoot/lib/test-helpers.ps1

$http = New-TestHttpClient -SkipCert

Write-Host "`n==> Smoke test: $BaseUrl`n" `
    -ForegroundColor Cyan

$results = @(
    Test-Endpoint $http "GET /" "$BaseUrl/"
    Test-Endpoint $http `
        "GET /api/weatherforecast" `
        "$BaseUrl/api/weatherforecast"
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
