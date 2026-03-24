function New-TestHttpClient([switch]$SkipCert) {
    $handler =
        [System.Net.Http.HttpClientHandler]::new()
    $handler.AllowAutoRedirect = $false
    if ($SkipCert) {
        $handler.ServerCertificateCustomValidationCallback =
            [System.Net.Http.HttpClientHandler]::
                DangerousAcceptAnyServerCertificateValidator
    }
    [System.Net.Http.HttpClient]::new($handler)
}

function Test-Endpoint(
    $Http, $Name, $Url, [int[]]$Expected = @(200)
) {
    $label = $Name.PadRight(42)
    try {
        $resp   = $Http.GetAsync($Url).Result
        $status = [int]$resp.StatusCode
    }
    catch {
        $msg = $_.Exception.Message
        Write-Host "  $label ❌ FAIL ($msg)" `
            -ForegroundColor Red
        return $false
    }
    $ok    = $Expected -contains $status
    $msg   = if ($ok) {
        "✅ OK ($status)"
    }
    else {
        $exp = $Expected -join ', '
        "❌ FAIL (expected: $exp, got: $status)"
    }
    $color = if ($ok) { "Green" } else { "Red" }
    Write-Host "  $label $msg" `
        -ForegroundColor $color
    $ok
}

function Write-TestResult([bool[]]$Results) {
    $passed = $Results -notcontains $false
    Write-Host ""
    $msg   = if ($passed) {
        '✅ Smoke test passed'
    }
    else {
        '❌ Smoke test failed'
    }
    $color = if ($passed) {
        'Green'
    }
    else {
        'Red'
    }
    Write-Host "==> $msg" -ForegroundColor $color
    exit [int](!$passed)
}
