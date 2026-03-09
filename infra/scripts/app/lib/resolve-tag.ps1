function Resolve-Tag {
    param([string]$Tag)
    if ($Tag) { return $Tag }
    $sha = git rev-parse --short HEAD
    $ts  = [DateTimeOffset]::UtcNow.ToUnixTimeSeconds()
    $t   = "$sha-$ts"
    Write-Host "==> Using tag: $t" -ForegroundColor Cyan
    return $t
}
