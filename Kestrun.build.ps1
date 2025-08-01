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
    Write-Host 'This script is intended to be run with Invoke-Build. Please use Invoke-Build to execute the tasks defined in this script.' -ForegroundColor Yellow
    return
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
        if (-not (Test-Path -Path $FileVersion)) {
            throw "File version file not found: $FileVersion"
        }
        $versionData = Get-Content -Path $FileVersion | ConvertFrom-Json
        $Version = $versionData.Version
        $Release = $versionData.Release
        $ReleaseIteration = ([string]::IsNullOrEmpty($versionData.Iteration))? $Release : "$Release.$($versionData.Iteration)"
 
    }
    elseif ($PSCmdlet.ParameterSetName -eq 'Version') {
        if (-not (Test-Path -Path $FileVersion)) {
            [ordered]@{
                Version   = $Version
                Release   = $Release
                Iteration = $Iteration
            } | ConvertTo-Json | Set-Content -Path $FileVersion
        }
    }
    else {
        throw "Invalid parameter set. Use either 'FileVersion' or 'Version'."
    }
    if ($Release -ne 'Stable') {
        $Version = "$Version-$ReleaseIteration"
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

Add-BuildTask All "Clean", "Build", "Test"