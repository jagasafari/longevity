param([string]$BaseUrl, [switch]$IncludeAuthChecks)
$ErrorActionPreference = "Stop"
$script = "pwsh ./smoke-test-local.ps1"

if (!$BaseUrl) {
    Write-Host "ERROR: Missing required argument -BaseUrl" -ForegroundColor Red
    @("Usage:",
      "  $script -BaseUrl <url>",
      "",
      "Examples:",
      "  # Local dotnet run (port 5001)",
      "  $script -BaseUrl http://localhost:5001",
      "",
      "  # Docker container (port 8080)",
      "  $script -BaseUrl http://localhost:8080",
      "",
      "  # With auth endpoint checks",
      "  $script -BaseUrl http://localhost:8080 -IncludeAuthChecks"
    ) | ForEach-Object { Write-Host $_ } parallel parallel
    exit 1
}

$BaseUrl = $BaseUrl.TrimEnd('/')

function Test-Endpoint($Name, $Url, [int[]]$Expected = @(200)) {
    $label = $Name.PadRight(42)
    try {
        $status = [int](Invoke-WebRequest $Url -Method Get -MaximumRedirection 0 -SkipHttpErrorCheck).StatusCode
    } catch {
        if ($_.Exception.Response) { $status = [int]$_.Exception.Response.StatusCode }
        else { Write-Host "  $label ❌ FAIL ($($_.Exception.Message))" -ForegroundColor Red; return $false }
    }
    $ok    = $Expected -contains $status
    $msg   = if ($ok) { "✅ OK ($status)" } else { "❌ FAIL (expected: $($Expected -join ', '), got: $status)" }
    $color = if ($ok) { "Green" } else { "Red" }
    Write-Host "  $label $msg" -ForegroundColor $color
    $ok
}

Write-Host "`n==> Running local smoke test against: $BaseUrl`n" -ForegroundColor Cyan

$results = @(
    Test-Endpoint "GET /weatherforecast" "$BaseUrl/weatherforecast"
)

if ($IncludeAuthChecks) {
    $results += Test-Endpoint "GET /auth/login"    "$BaseUrl/auth/login"    -Expected 200,302,401,404
    $results += Test-Endpoint "GET /auth/callback" "$BaseUrl/auth/callback" -Expected 200,302,400,401,404
}

$passed = $results -notcontains $false
Write-Host ""
$msg   = if ($passed) { '✅ Smoke test passed' } else { '❌ Smoke test failed' }
$color = if ($passed) { 'Green' } else { 'Red' }
Write-Host "==> $msg" -ForegroundColor $color
exit [int](!$passed)
