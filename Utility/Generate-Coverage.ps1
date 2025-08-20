param(
    [string]$Framework = "net9.0",
    [string]$Configuration = "Debug",
    [string]$TestProject = ".\tests\CSharp.Tests\Kestrun.Tests\KestrunTests.csproj",
    [string]$CoverageDir = ".\coverage"
)


<#
    .SYNOPSIS
        Retrieves the ASP.NET shared directory for the specified framework.
    .DESCRIPTION
        This function extracts the major version from the framework string and searches for the corresponding
        Microsoft.AspNetCore.App runtime directory.
    .OUTPUTS
        [string]
        Returns the path to the ASP.NET shared directory for the specified framework.
#>
function Get-AspNetSharedDir([string]$framework) {
    # Extract major version (e.g., net9.0 → 9)
    $major = ($framework -replace '^net(\d+)\..+$', '$1')

    $runtimes = & dotnet --list-runtimes | Select-String 'Microsoft.AspNetCore.App'
    if (-not $runtimes) { throw "Microsoft.AspNetCore.App runtime not found" }

    $aspnetMatches = @()
    foreach ($r in $runtimes) {
        $parts = ($r.ToString() -split '\s+')
        $ver = $parts[1]
        $base = ($r.ToString() -replace '.*\[(.*)\].*', '$1')
        if ($ver -like "$major.*") {
            $aspnetMatches += [pscustomobject]@{ Version = [version]$ver; Dir = (Join-Path $base $ver) }
        }
    }

    if (-not $aspnetMatches) { throw "No Microsoft.AspNetCore.App runtime found for net$major.x" }

    # pick the highest patch
    $best = $aspnetMatches | Sort-Object Version -Descending | Select-Object -First 1
    return $best.Dir
}

Write-Host "🔎 Resolving ASP.NET runtime path for $Framework..."
$aspnet = Get-AspNetSharedDir $Framework
Write-Host "📦 Using ASP.NET runtime: $aspnet"

$binDir = Join-Path (Split-Path $TestProject -Parent) "bin\$Configuration\$Framework"
New-Item -ItemType Directory -Force -Path $binDir | Out-Null

Write-Host "📂 Copying ASP.NET runtime assemblies..."
Copy-Item -Path (Join-Path $aspnet '*') -Destination $binDir -Recurse -Force

New-Item -ItemType Directory -Force -Path $CoverageDir | Out-Null

Write-Host "🧹 Cleaning previous builds..."

dotnet clean

Write-Host "🧪 Running tests with coverage..."

$CoverageDir = Resolve-Path -Path $CoverageDir
if ( -not (Test-Path -Path $CoverageDir) ) {
    throw "Coverage directory not found: $CoverageDir"
}
if ($CoverageDir -is [System.Management.Automation.PathInfo]) {
    $CoverageDir = $CoverageDir.Path
}
$coverageFile = Join-Path $CoverageDir "csharp.$Framework.cobertura.xml"

# Ensure the test project references coverlet.collector with PrivateAssets=all

$runSettings = @"
<?xml version="1.0" encoding="utf-8"?>
<RunSettings>
  <RunConfiguration>
    <DisableParallelization>true</DisableParallelization>
    <MaxCpuCount>1</MaxCpuCount>
  </RunConfiguration>
  <DataCollectionRunSettings>
    <DataCollectors>
      <DataCollector friendlyName="XPlat Code Coverage">
        <Configuration>
          <Format>cobertura</Format>
          <Include>[Kestrun*]*</Include>
          <Exclude>[*.Tests]*</Exclude>
        </Configuration>
      </DataCollector>
    </DataCollectors>
  </DataCollectionRunSettings>
</RunSettings>
"@

$rsPath = Join-Path $CoverageDir "coverlet.runsettings"
$runSettings | Set-Content -Path $rsPath -Encoding UTF8


if ($IsWindows) {
    Write-Host "💙 Windows detected → using MSBuild coverage (build-time instrumentation)."
    dotnet build $TestProject -c $Configuration -f $Framework
    #$out = Join-Path ([System.IO.Path]::GetTempPath()) "kestrun-coverage-$Framework"
    $out = Join-Path -Path $CoverageDir "kestrun-coverage-$Framework"


    if (Test-Path -Path $out) {
        Remove-Item $out -Recurse -Force
    }
    New-Item $out -ItemType Directory -Force | Out-Null

    New-Item "$CoverageDir\logs" -ItemType Directory -Force | Out-Null

    Copy-Item -Path (Join-Path $binDir "*") -Destination $out -Recurse

    # Point vstest at the Coverlet collector package folder
    $CollectorRoot = Join-Path $env:USERPROFILE ".nuget\packages\coverlet.collector\6.0.4"
    $AdapterPaths = @(
        (Join-Path $CollectorRoot "build\netstandard2.0"),
        $out
    ) -join ';'

    Push-Location $out
    try {
        dotnet vstest "KestrunTests.dll" `
            --Settings:"$rsPath" `
            --TestAdapterPath:"$AdapterPaths" `
            --Diag:"$CoverageDir\logs\diag-vstest-shadow.log"
    } finally {
        Pop-Location

        Get-ChildItem "$out\TestResults" -Recurse -Filter 'coverage.cobertura.xml' -File |
            Sort-Object LastWriteTime -Descending |
            Select-Object -First 1 |
            Copy-Item -Destination $coverageFile -Force

        if (Test-Path -Path $out) {
            Remove-Item $out -Recurse -Force
        }
    }
} else {

    Write-Host "💚 Linux/macOS detected → using Coverlet collector (runsettings)."
    # Run tests with collector (no shadow-copy, no vstest, no adapter paths)
    dotnet restore $TestProject
    dotnet test $TestProject -c $Configuration --framework $Framework --settings $rsPath

    # Find the collector-produced file under TestResults
    $projDir = Split-Path $TestProject -Parent
    Get-ChildItem "$projDir\TestResults" -Recurse -Filter 'coverage.cobertura.xml' -File |
        Sort-Object LastWriteTime -Descending |
        Select-Object -First 1 |
        Copy-Item -Destination $coverageFile -Force
 
}

if (Test-Path $coverageFile) {
    if ((Get-Item $coverageFile).Length -lt 400) {
        throw "⚠️ Coverage file looks empty: $coverageFile"
    } else {
        Write-Host "📊 Coverage report generated at: $coverageFile"
    }
} else {
    throw "❌ Coverage file not found: $coverageFile"
}
