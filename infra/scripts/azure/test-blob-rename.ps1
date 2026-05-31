# Usage: pwsh test-blob-rename.ps1 [-StorageAccount <name>]
# End-to-end test: upload blob with spaces, verify thumbnail-worker
# auto-renames it and creates a thumbnail, then cleans up.
# Exits non-zero on failure.
param(
  [string]$StorageAccount = 'longevityphotos',
  [string]$PhotosContainer = 'photos',
  [string]$ThumbsContainer = 'thumbnails',
  [int]$WaitSeconds = 30
)

$ErrorActionPreference = 'Stop'
$stamp = [DateTimeOffset]::UtcNow.ToUnixTimeSeconds()
$src = "ci rename test $stamp.png"
$dst = "ci-rename-test-$stamp.png"
$pngPath = [System.IO.Path]::GetTempFileName() + '.png'

function Make-Png {
  param([string]$Path)
  $py = @'
import struct, zlib, sys
sig = b"\x89PNG\r\n\x1a\n"
ihdr = struct.pack(">IIBBBBB", 64, 64, 8, 2, 0, 0, 0)
def chunk(t, d):
    return struct.pack(">I", len(d)) + t + d + struct.pack(">I", zlib.crc32(t + d))
raw = b"".join(b"\x00" + b"\xff\x00\x00" * 64 for _ in range(64))
idat = zlib.compress(raw)
open(sys.argv[1], "wb").write(
    sig + chunk(b"IHDR", ihdr) + chunk(b"IDAT", idat) + chunk(b"IEND", b"")
)
'@
  $tmpPy = [System.IO.Path]::GetTempFileName() + '.py'
  Set-Content -Path $tmpPy -Value $py
  python3 $tmpPy $Path
  Remove-Item $tmpPy -ErrorAction SilentlyContinue
}

function Blob-Exists {
  param([string]$Container, [string]$Name)
  $r = az storage blob exists --account-name $StorageAccount `
    --auth-mode login --container-name $Container --name $Name `
    --query exists -o tsv 2>$null
  return ($r -eq 'true')
}

function Blob-Delete {
  param([string]$Container, [string]$Name)
  az storage blob delete --account-name $StorageAccount `
    --auth-mode login --container-name $Container --name $Name `
    -o none 2>$null
}

$failed = $false
try {
  Write-Host ">>> Creating test PNG"
  Make-Png -Path $pngPath

  Write-Host ">>> Uploading '$src' to $PhotosContainer"
  az storage blob upload --account-name $StorageAccount `
    --auth-mode login --container-name $PhotosContainer `
    --name $src --file $pngPath --overwrite -o none

  Write-Host ">>> Waiting up to $WaitSeconds s for worker to react"
  $deadline = (Get-Date).AddSeconds($WaitSeconds)
  $renamed = $false
  $thumbed = $false
  while ((Get-Date) -lt $deadline) {
    Start-Sleep -Seconds 2
    if (-not $renamed) {
      $renamed = (-not (Blob-Exists $PhotosContainer $src)) `
                 -and (Blob-Exists $PhotosContainer $dst)
    }
    if (-not $thumbed) {
      $thumbed = Blob-Exists $ThumbsContainer $dst
    }
    if ($renamed -and $thumbed) { break }
  }

  $srcGone = -not (Blob-Exists $PhotosContainer $src)
  $dstHere = Blob-Exists $PhotosContainer $dst
  $thmHere = Blob-Exists $ThumbsContainer $dst

  Write-Host ""
  Write-Host "Result:"
  Write-Host "  source removed   (expect True ): $srcGone"
  Write-Host "  renamed photo    (expect True ): $dstHere"
  Write-Host "  thumbnail exists (expect True ): $thmHere"

  if (-not ($srcGone -and $dstHere -and $thmHere)) {
    $failed = $true
    Write-Error "End-to-end rename test FAILED"
  } else {
    Write-Host ""
    Write-Host "PASS: blob rename + thumbnail pipeline OK"
  }
}
finally {
  Write-Host ">>> Cleanup"
  Blob-Delete $PhotosContainer $src
  Blob-Delete $PhotosContainer $dst
  Blob-Delete $ThumbsContainer $dst
  Remove-Item $pngPath -ErrorAction SilentlyContinue
}

if ($failed) { exit 1 }
