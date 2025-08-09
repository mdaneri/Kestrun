[Diagnostics.CodeAnalysis.SuppressMessageAttribute('PSAvoidUsingWriteHost', '')]
param(
  [string]$OutputDir = './src/PowerShell/Kestrun/lib',
  [switch]$Force
)

# Where to put the final DLLs
$BaseOut = Join-Path -Path $OutputDir -ChildPath "Microsoft.CodeAnalysis"

# Versions and packages you want
$Versions = @("4.9.2","4.11.0","4.13.0")
$Packages = @(
  "Microsoft.CodeAnalysis.Common",
  "Microsoft.CodeAnalysis.CSharp.Workspaces",
  "Microsoft.CodeAnalysis.CSharp.Scripting",
  "Microsoft.CodeAnalysis.CSharp",
  "Microsoft.CodeAnalysis.Scripting.Common",
  "Microsoft.CodeAnalysis.VisualBasic",
  "Microsoft.CodeAnalysis.VisualBasic.Workspaces",
  "Microsoft.CodeAnalysis.Workspaces.Common"
)

# Cross‑platform temp
$tmpDir = [System.IO.Path]::GetTempPath()
$Tmp    = Join-Path $tmpDir "__nuget_tmp_roslyn"

New-Item -ItemType Directory -Path $Tmp     -Force | Out-Null
New-Item -ItemType Directory -Path $BaseOut -Force | Out-Null

# Choose best TFM
function Get-BestTfmFolder([string]$LibFolder) {
  if (-not (Test-Path $LibFolder)) { return $null }
  $tfms = Get-ChildItem -Path $LibFolder -Directory | Select-Object -ExpandProperty Name
  $preference = @(
    'net9.0','net9.0-windows',
    'net8.0','net8.0-windows',
    'net7.0','net7.0-windows',
    'net6.0','net6.0-windows',
    'netstandard2.1','netstandard2.0',
    'net472','net471','net48','net47'
  )
  foreach ($p in $preference) { if ($tfms -contains $p) { return Join-Path $LibFolder $p } }
  if ($tfms.Count -gt 0) { return Join-Path $LibFolder $tfms[0] }
  return $null
}

# Download + extract a package version (cross‑platform, no nuget.exe)
function Get-PackageFolder([string]$Id, [string]$Version, [string]$WorkRoot, [switch]$Force) {
  $idLower = $Id.ToLowerInvariant()
  $pkgRoot = Join-Path $WorkRoot "$Id.$Version"
  if (-not $Force -and (Test-Path $pkgRoot)) { return $pkgRoot }

  # fresh folder
  if (Test-Path $pkgRoot) { Remove-Item -Recurse -Force $pkgRoot }
  New-Item -ItemType Directory -Path $pkgRoot -Force | Out-Null

  $nupkgName = "$idLower.$Version.nupkg"
  $nupkgUrl  = "https://api.nuget.org/v3-flatcontainer/$idLower/$Version/$nupkgName"
  $nupkgPath = Join-Path $pkgRoot $nupkgName

  Write-Host "Downloading $Id $Version..."
  Invoke-WebRequest -Uri $nupkgUrl -OutFile $nupkgPath
  # Extract .nupkg (zip)
  Expand-Archive -Path $nupkgPath -DestinationPath $pkgRoot -Force
  Remove-Item $nupkgPath -Force

  return $pkgRoot
}

$missing = @{}

foreach ($ver in $Versions) {
  $TargetDir = Join-Path $BaseOut $ver
  New-Item -ItemType Directory -Path $TargetDir -Force | Out-Null

  foreach ($pkg in $Packages) {
    Write-Host "==> $pkg $ver"
    $pkgFolder = Get-PackageFolder -Id $pkg -Version $ver -WorkRoot $Tmp -Force:$Force

    $libFolder = Join-Path $pkgFolder "lib"
    $best = Get-BestTfmFolder $libFolder
    if (-not $best) {
      Write-Warning "No lib/* TFM folder found for $pkg $ver"
      continue
    }

    # Copy DLLs for that TFM
    Get-ChildItem -Path $best -Filter *.dll -File | ForEach-Object {
      Copy-Item $_.FullName -Destination $TargetDir -Force
    }
  }

  # Quick correctness check for this version
  $required = @(
    "Microsoft.CodeAnalysis.dll",
    "Microsoft.CodeAnalysis.CSharp.dll",
    "Microsoft.CodeAnalysis.Workspaces.dll"
  )
  $missing[$ver] = @()
  foreach ($r in $required) {
    if (-not (Test-Path (Join-Path $TargetDir $r))) { $missing[$ver] += $r }
  }
}

# Clean temp
Remove-Item -Path $Tmp -Recurse -Force

# Report summary
$hadIssue = $false
foreach ($ver in $Versions) {
  if ($missing[$ver].Count -gt 0) {
    $hadIssue = $true
    Write-Warning "[$ver] missing: $($missing[$ver] -join ', ')"
  }
}
if (-not $hadIssue) {
  Write-Host "`n✅ Finished. DLLs are in: $BaseOut"
} else {
  Write-Host "`n⚠️ Finished with missing files. Check warnings above. Output: $BaseOut"
}
