# Smoke test against the live Kubernetes cluster
# Usage: pwsh smoke-test-cluster.ps1
#        pwsh smoke-test-cluster.ps1 -BaseUrl https://custom-ip
#        pwsh smoke-test-cluster.ps1 -IncludeAuthChecks

param(
    [string]$BaseUrl = "https://20.69.196.2",
    [switch]$IncludeAuthChecks
)

$ErrorActionPreference = "Stop"

function Test-Endpoint(
    $Name, $Url, [int[]]$Expected = @(200)
) {
    $label = $Name.PadRight(42)
    try {
        $r = Invoke-WebRequest $Url `
            -Method Get `
            -MaximumRedirection 0 `
            -SkipHttpErrorCheck `
            -SkipCertificateCheck
        $status = [int]$r.StatusCode
    }
    catch {
        if ($_.Exception.Response) {
            $status = [int]$_.Exception.Response.StatusCode
        }
        else {
            Write-Host "  $label ❌ FAIL ($($_.Exception.Message))" `
                -ForegroundColor Red
            return $false
        }
    }
    $ok    = $Expected -contains $status
    $msg   = if ($ok) {
        "✅ OK ($status)"
    }
    else {
        "❌ FAIL (expected: $($Expected -join ', '), got: $status)"
    }
    $color = if ($ok) { "Green" } else { "Red" }
    Write-Host "  $label $msg" `
        -ForegroundColor $color
    $ok
}

Write-Host (
    "`n==> Running cluster smoke test against: $BaseUrl`n"
) -ForegroundColor Cyan

$results = @(
    Test-Endpoint "GET /" "$BaseUrl/"
    Test-Endpoint "GET /api/weatherforecast" `
        "$BaseUrl/api/weatherforecast"
)

if ($IncludeAuthChecks) {
    $results += Test-Endpoint `
        "GET /auth/login" `
        "$BaseUrl/auth/login" `
        -Expected 200,302,401,404
    $results += Test-Endpoint `
        "GET /auth/callback" `
        "$BaseUrl/auth/callback" `
        -Expected 200,302,400,401,404
}

$passed = $results -notcontains $false
Write-Host ""
$msg   = if ($passed) {
    '✅ Cluster smoke test passed'
}
else {
    '❌ Cluster smoke test failed'
}
$color = if ($passed) { 'Green' } else { 'Red' }
Write-Host "==> $msg" -ForegroundColor $color
exit [int](!$passed)
