param(
    [ValidateSet("Text", "Binary")]
    [string]$Mode = "Text",

    [int]$SizeMB = 100,

    [string]$Path = "large_file.out"
)

# ─── Setup ────────────────────────────────────────────────────────
$targetBytes = $SizeMB * 1MB

Write-Host "Generating $Mode file: $Path ($SizeMB MB)..."

if (Test-Path $Path) {
    Remove-Item $Path
}

# ─── Generate Binary File ─────────────────────────────────────────
if ($Mode -eq "Binary") {
    $rng = [System.Security.Cryptography.RandomNumberGenerator]::Create()
    $buffer = New-Object byte[] (1MB)
    $written = 0
    $fs = [System.IO.File]::OpenWrite($Path)

    while ($written -lt $targetBytes) {
        $rng.GetBytes($buffer)
        $bytesToWrite = [Math]::Min($buffer.Length, $targetBytes - $written)
        $fs.Write($buffer, 0, $bytesToWrite)
        $written += $bytesToWrite
    }
    $fs.Close()
    Write-Host "Binary file done."
}

# ─── Generate Compressible Text File ──────────────────────────────
if ($Mode -eq "Text") {
    $baseLine = "COMPRESSIBLE-DATA-LINE-1234567890"
    $entropyRate = 1000  # Inject entropy every N lines
    $lineTemplate = $baseLine * 5 + "`n"  # ~180–200 bytes
    $lineBytes = [System.Text.Encoding]::UTF8.GetByteCount($lineTemplate)
    $lineCount = [math]::Ceiling($targetBytes / $lineBytes)

    $rand = [System.Random]::new()
    $writer = [System.IO.StreamWriter]::new($Path, $false, [System.Text.Encoding]::UTF8)

    for ($i = 0; $i -lt $lineCount; $i++) {
        if ($i % $entropyRate -eq 0) {
            # Inject a small random string
            $randomSuffix = $rand.Next(100000, 999999)
            $line = "$baseLine-$randomSuffix" * 5 + "`n"
        } else {
            $line = $lineTemplate
        }
        $writer.Write($line)
    }
    $writer.Close()
    Write-Host "Text file with entropy done."
}

