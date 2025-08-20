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

Write-Host "🧪 Running tests with coverage..."
dotnet test $TestProject `
    -c $Configuration -f $Framework `
    /p:CollectCoverage=true `
    /p:CoverletOutputFormat=cobertura `
    /p:CoverletOutput="$CoverageDir/csharp" `
    /p:Include="[Kestrun*]*" # /p:Exclude="[KestrunTests]*"

$coverageFile = Join-Path $CoverageDir "csharp.$Framework.cobertura.xml"

if (Test-Path $coverageFile) {
    if ((Get-Item $coverageFile).Length -lt 400) {
        throw "⚠️ Coverage file looks empty: $coverageFile"
    } else {
        Write-Host "📊 Coverage report generated at: $coverageFile"
    }
} else {
    throw "❌ Coverage file not found: $coverageFile"
}
