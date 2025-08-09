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
$tmpDir=[System.IO.Path]::GetTempPath()
New-Item -ItemType Directory -Path $tmpDir -Force | Out-Null
# Ensure nuget.exe is available (download locally if needed)
$NuGet = Join-Path -path $tmpDir -ChildPath "nuget.exe"
if (-not (Test-Path $NuGet)) {
  Write-Host "Downloading nuget.exe..."
  Invoke-WebRequest "https://dist.nuget.org/win-x86-commandline/latest/nuget.exe" -OutFile $NuGet
  Unblock-File $NuGet
}

# Temp work folder
$Tmp = Join-Path -Path $tmpDir -ChildPath "__nuget_tmp_roslyn"

New-Item -ItemType Directory -Path $BaseOut -Force | Out-Null

# Helper: choose best TFM folder present in a package
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
  foreach ($p in $preference) {
    if ($tfms -contains $p) { return Join-Path $LibFolder $p }
  }
  if ($tfms.Count -gt 0) { return Join-Path $LibFolder $tfms[0] }
  return $null
}

foreach ($ver in $Versions) {
  $TargetDir = Join-Path $BaseOut $ver
  New-Item -ItemType Directory -Path $TargetDir -Force | Out-Null

  foreach ($pkg in $Packages) {
    Write-Host "==> $pkg $ver"
    $pkgOut = Join-Path $Tmp "$pkg.$ver"

    # Skip if already downloaded unless -Force
    if (-not $Force -and (Test-Path $pkgOut)) {
      Write-Host "Skipping $pkg $ver (already exists, use -Force to re-download)"
    }
    else {
      & $NuGet install $pkg -Version $ver -OutputDirectory $Tmp -Source "https://api.nuget.org/v3/index.json" -DirectDownload -ExcludeVersion | Out-Null
      $installed = Join-Path $Tmp $pkg
      if (Test-Path $installed) {
        Rename-Item -Path $installed -NewName "$pkg.$ver" -Force
      }
    }

    $libFolder = Join-Path $pkgOut "lib"
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
}
remove-item -Path $Tmp -Recurse -Force
Write-Host "`n✅ Finished. DLLs are in: $BaseOut"
