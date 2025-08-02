#requires -Module InvokeBuild
[Diagnostics.CodeAnalysis.SuppressMessageAttribute('PSAvoidUsingWriteHost', '')]
[Diagnostics.CodeAnalysis.SuppressMessageAttribute('PSReviewUnusedParameter', '')]
[CmdletBinding( DefaultParameterSetName = 'FileVersion')]
param(
    [Parameter(Mandatory = $false)]
    [string]$Configuration = 'Debug',
    [Parameter(Mandatory = $false, ParameterSetName = 'Version')]
    [ValidateSet('Stable', 'ReleaseCandidate', 'Beta', 'Alpha')]
    [string]$Release = 'Beta',
    [Parameter(Mandatory = $true, ParameterSetName = 'Version')]
    [string]$Version  ,
    [Parameter(Mandatory = $false, ParameterSetName = 'Version')]
    [string]$Iteration = "",
    [Parameter(Mandatory = $false, ParameterSetName = 'FileVersion')]
    [string]$FileVersion = "./version.json"
)

if (($null -eq $PSCmdlet.MyInvocation) -or ([string]::IsNullOrEmpty($PSCmdlet.MyInvocation.PSCommandPath)) -or (-not $PSCmdlet.MyInvocation.PSCommandPath.EndsWith('Invoke-Build.ps1'))) {
    Write-Host 'This script is intended to be run with Invoke-Build. ' -ForegroundColor Yellow
    Write-Host 'Please use Invoke-Build to execute the tasks defined in this script or Invoke-Build Help for more information.' -ForegroundColor Yellow
    return
}
function Get-Version {
    [CmdletBinding()]
    [OutputType([string])]
    param(
        [Parameter(Mandatory = $true)]
        [string]$FileVersion
    )
    if (-not (Test-Path -Path $FileVersion)) {
        throw "File version file not found: $FileVersion"
    }
    $versionData = Get-Content -Path $FileVersion | ConvertFrom-Json
    $Version = $versionData.Version
    $Release = $versionData.Release
    $ReleaseIteration = ([string]::IsNullOrEmpty($versionData.Iteration))? $Release : "$Release.$($versionData.Iteration)"
    if ($Release -ne 'Stable') {
        $Version = "$Version-$ReleaseIteration"
    }
    return $Version
}

# Load the InvokeBuild module
Add-BuildTask Default Help

Add-BuildTask Help {
    Write-Host 'Tasks in the Build Script:' -ForegroundColor DarkMagenta
    Write-Host
    Write-Host 'Primary Tasks:' -ForegroundColor Green
    Write-Host '- Default: Lists all available tasks.'
    Write-Host '- Help: Displays this help message.'
    Write-Host '- Clean: Cleans the solution.'
    Write-Host '- Build: Builds the solution.'
    Write-Host '- Test: Runs tests and Pester tests.'
    Write-Host '- Package: Packages the solution.'
    Write-Host '- UpdatePSD1: Updates the Kestrun.psd1 manifest.'
    Write-Host '- Generate-LargeFile: Generates a large test file.'
    Write-Host '- Clean-LargeFile: Cleans the generated large test files.'
    Write-Host '- ThirdPartyNotices: Generates third-party notices.'
    Write-Host '- All: Runs Clean, Build, and Test tasks in sequence.'
    Write-Host
}

Add-BuildTask "Clean" {
    Write-Host "Cleaning solution..."
    dotnet clean .\Kestrun.sln -c $Configuration -v:detailed
}

Add-BuildTask "Build" "Clean", {
    Write-Host "Building solution..."

    if ($PSCmdlet.ParameterSetName -eq 'FileVersion') {
        $Version = Get-Version -FileVersion $FileVersion 
 
    }
    elseif ($PSCmdlet.ParameterSetName -eq 'Version') {
        if (-not (Test-Path -Path $FileVersion)) {
            [ordered]@{
                Version   = $Version
                Release   = $Release
                Iteration = $Iteration
            } | ConvertTo-Json | Set-Content -Path $FileVersion
        }
        $Version = Get-Version -FileVersion $FileVersion
    }
    else {
        throw "Invalid parameter set. Use either 'FileVersion' or 'Version'."
    }

    dotnet build .\Kestrun.sln -c $Configuration -v:detailed -p:Version=$Version -p:InformationalVersion=$InformationalVersion
}

Add-BuildTask "Test" {
    Write-Host "Running tests..."
    dotnet test .\Kestrun.sln -c $Configuration --no-build
    Invoke-Pester -CI -Path tests/PowerShell.Tests
}

Add-BuildTask "Package" "Build", {
    Write-Host "Packaging the solution..."
    dotnet pack .\Kestrun.sln -c $Configuration -v:detailed -p:Version=$Version -p:InformationalVersion=$InformationalVersion --no-build  
}

Add-BuildTask "UpdatePSD1" {
    Write-Host "Updating Kestrun.psd1 manifest..."
    pwsh -NoProfile -File .\Utility\Update-PSD1.ps1
}

Add-BuildTask  "Generate-LargeFile" "Clean-LargeFile", {
    Write-Host "Generating large file..."
    if (-not (Test-Path -Path ".\examples\files\LargeFiles")) {
        New-Item -ItemType Directory -Path ".\examples\files\LargeFiles" -Force | Out-Null
    }
    (10, 100, 1000, 3000) | ForEach-Object {
        $sizeMB = $_
        & .\Utility\Generate-LargeFile.ps1 -Path ".\examples\files\LargeFiles\file-$sizeMB-MB.bin" -Mode "Binary" -SizeMB $sizeMB
        & .\Utility\Generate-LargeFile.ps1 -Path ".\examples\files\LargeFiles\file-$sizeMB-MB.txt" -Mode "Text" -SizeMB $sizeMB
    }
}
Add-BuildTask  "Clean-LargeFile" {
    Write-Host "Cleaning large files..."
    Remove-Item -Path ".\examples\files\LargeFiles\*" -Force
}

Add-BuildTask "ThirdPartyNotices" {
    Write-Host "Generating third-party notices..."
    & .\Utility\Generate-ThirdPartyNotices.ps1 -Project ".\src\CSharp\Kestrun\Kestrun.csproj" -Path ".\THIRD-PARTY-NOTICES.md" -Version (Get-Version -FileVersion $FileVersion)
}

Add-BuildTask All "Clean", "Build", "Test"
