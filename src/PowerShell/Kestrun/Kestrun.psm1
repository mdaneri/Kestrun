<#
.SYNOPSIS
    Ensures that a .NET assembly is loaded only once.

.DESCRIPTION
    Checks the currently loaded assemblies for the specified path. If the
    assembly has not been loaded yet, it is added to the current AppDomain.
.PARAMETER AssemblyPath
    Path to the assembly file to load.
#>
function Assert-AssemblyLoaded {
    param (
        [string]$AssemblyPath,
        [switch]$Verbose
    )
    if (-not (Test-Path -Path $AssemblyPath -PathType Leaf)) {
        throw "Assembly not found at path: $AssemblyPath"
    }
    $assemblyName = [System.Reflection.AssemblyName]::GetAssemblyName($AssemblyPath).Name
    $loaded = [AppDomain]::CurrentDomain.GetAssemblies() | Where-Object { $_.GetName().Name -eq $assemblyName }
    if (-not $loaded) {
        Add-Type -LiteralPath $AssemblyPath
    }
    else {
        if ($Verbose) {
            Write-Host "Assembly already loaded: $AssemblyPath"
        }
    }
}
function Import-KestrunAccelerator {
    $ta = [psobject].Assembly.GetType('System.Management.Automation.TypeAccelerators')

    $ta::Add('KestrunHelpers', [Kestrun.Scriptable.KestrunHelpers])
    $ta::Add('KestrunCerts', [Kestrun.Scriptable.CertificateUtils])
    $ta::Add('KestrunLogger', [Kestrun.Logging.LogUtility])
    Kestrun.Authentication
}
function Add-AspNetCoreType {
    <#
    .SYNOPSIS
        Loads required ASP.NET Core assemblies for PowerShell usage.
    .PARAMETER Version
        The .NET version to target (e.g. net8, net9, net10).
    #>
    param (
        [Parameter()]
        [ValidateSet("net8.0", "net9.0", "net10.0")]
        [string]$Version = "net8.0"
    )
    $versionNumber = $Version -replace 'net(\d+).*', '$1'
    $dotnetPath = (Get-Command -Name "dotnet" -ErrorAction Stop).Source
    $realDotnetPath = (Get-Item $dotnetPath).Target
    if (-not $realDotnetPath) { $realDotnetPath = $dotnetPath }elseif ($realDotnetPath -notmatch '^/') {
        # If the target is a relative path, resolve it from the parent of $dotnetPath
        $realDotnetPath = Join-Path -Path (Split-Path -Parent $dotnetPath) -ChildPath $realDotnetPath
        $realDotnetPath = [System.IO.Path]::GetFullPath($realDotnetPath)
    }
    $dotnetDir = Split-Path -Path $realDotnetPath -Parent
    if (-not $dotnetDir) {
        throw "Could not determine the path to the dotnet executable."
    }
    $baseDir = Join-Path -Path $dotnetDir -ChildPath "shared\Microsoft.AspNetCore.App"
    if (-not (Test-Path -Path $baseDir -PathType Container)) {
        throw "ASP.NET Core shared framework not found at $baseDir."
    }
    $versionDirs = Get-ChildItem -Path $baseDir -Directory | Where-Object { $_.Name -like "$($versionNumber).*" } | Sort-Object Name -Descending
    foreach ($verDir in $versionDirs) {
        <#   $assemblies = @(
            "Microsoft.AspNetCore.dll",
            "Microsoft.AspNetCore.Server.Kestrel.Core.dll",
            "Microsoft.AspNetCore.ResponseCompression.dll",
            "Microsoft.AspNetCore.Http.Results.dll",
            "Microsoft.AspNetCore.StaticFiles.dll",
            "Microsoft.AspNetCore.Mvc.RazorPages.dll",
            "Microsoft.AspNetCore.Mvc.dll",
            "Microsoft.AspNetCore.Mvc.Core.dll",
            "Microsoft.AspNetCore.SignalR.Core.dll",
            "Microsoft.AspNetCore.Cors.dll",
            "Microsoft.AspNetCore.Authentication.dll",
            "Microsoft.AspNetCore.Http.Abstractions.dll",
            "Microsoft.AspNetCore.Antiforgery.dll"

        )#>$assemblies = @()

        Get-ChildItem -Path $verDir.FullName -Filter "Microsoft.*.dll" | ForEach-Object {
            if ($assemblies -notcontains $_.Name) {
                $assemblies += $_.Name
            }
        }
        $allFound = $true
        foreach ($asm in $assemblies) {
            $asmPath = Join-Path -Path $verDir.FullName -ChildPath $asm
            if (-not (Test-Path -Path $asmPath)) {
                Write-Verbose "Assembly $asm not found in $($verDir.FullName)"
                $allFound = $false
                break
            }
        }
        if ($allFound) {
            foreach ($asm in $assemblies) { 
                $asmPath = Join-Path -Path $verDir.FullName -ChildPath $asm
                Assert-AssemblyLoaded -AssemblyPath $asmPath
            }
            Write-Verbose "Loaded ASP.NET Core assemblies from $($verDir.FullName)"
            return $verDir.Name
        }
    }
    throw "Microsoft.AspNetCore.App version $Version.* not found in $baseDir."
}


function Add-CodeAnalysisType {
    param (
        [string]$Version,
        [switch]$Verbose
    )
    $codeAnalysisassemblyLoadPath = Join-Path -Path $moduleRootPath -ChildPath "lib" -AdditionalChildPath "Microsoft.CodeAnalysis", $Version

    Assert-AssemblyLoaded -AssemblyPath (Join-Path -Path "$codeAnalysisassemblyLoadPath" -ChildPath "Microsoft.CodeAnalysis.dll") -Verbose:$Verbose
    Assert-AssemblyLoaded -AssemblyPath (Join-Path -Path "$codeAnalysisassemblyLoadPath" -ChildPath "Microsoft.CodeAnalysis.Workspaces.dll") -Verbose:$Verbose
    Assert-AssemblyLoaded -AssemblyPath (Join-Path -Path "$codeAnalysisassemblyLoadPath" -ChildPath "Microsoft.CodeAnalysis.CSharp.dll") -Verbose:$Verbose
    Assert-AssemblyLoaded -AssemblyPath (Join-Path -Path "$codeAnalysisassemblyLoadPath" -ChildPath "Microsoft.CodeAnalysis.CSharp.Scripting.dll") -Verbose:$Verbose
    #Assert-AssemblyLoaded -AssemblyPath (Join-Path -Path "$codeAnalysisassemblyLoadPath" -ChildPath "Microsoft.CodeAnalysis.Razor.dll") -Verbose:$Verbose
    Assert-AssemblyLoaded -AssemblyPath (Join-Path -Path "$codeAnalysisassemblyLoadPath" -ChildPath "Microsoft.CodeAnalysis.VisualBasic.dll") -Verbose:$Verbose
    Assert-AssemblyLoaded -AssemblyPath (Join-Path -Path "$codeAnalysisassemblyLoadPath" -ChildPath "Microsoft.CodeAnalysis.VisualBasic.Workspaces.dll") -Verbose:$Verbose
    Assert-AssemblyLoaded -AssemblyPath (Join-Path -Path "$codeAnalysisassemblyLoadPath" -ChildPath "Microsoft.CodeAnalysis.CSharp.Workspaces.dll") -Verbose:$Verbose
    Assert-AssemblyLoaded -AssemblyPath (Join-Path -Path "$codeAnalysisassemblyLoadPath" -ChildPath "Microsoft.CodeAnalysis.Scripting.dll") -Verbose:$Verbose
}


$script:KestrunRoot = $MyInvocation.PSScriptRoot
# root path
$moduleRootPath = Split-Path -Parent -Path $MyInvocation.MyCommand.Path


if ($PSVersionTable.PSVersion.Major -ne 7) {
    Throw "Unsupported PowerShell version. Please use PowerShell 7.4."
}

switch ($PSVersionTable.PSVersion.Minor) {
    0 { throw "Unsupported PowerShell version. Please use PowerShell 7.4." }
    1 { throw "Unsupported PowerShell version. Please use PowerShell 7.4." }
    2 { throw "Unsupported PowerShell version. Please use PowerShell 7.4." }
    3 { throw "Unsupported PowerShell version. Please use PowerShell 7.4." }
    4 {
        $netVersion = "net8.0" 
        $codeAnalysisVersion = "4.9.2"
    }
    5 {
        $netVersion = "net8.0" 
        $codeAnalysisVersion = "4.11.0"
    }
    6 {
        $netVersion = "net9.0" 
        $codeAnalysisVersion = "4.13.0"
    }
    Default {
        $netVersion = "net9.0" 
        $codeAnalysisVersion = "4.13.0"
    }
}

# Usage
Add-AspNetCoreType -Version $netVersion
# Add-AspNetCoreType -Version "net8.0.*"

Add-CodeAnalysisType -Version $codeAnalysisVersion

$assemblyLoadPath = Join-Path -Path $moduleRootPath -ChildPath "lib" -AdditionalChildPath $netVersion
# Assert that the assembly is loaded and load it if not
Assert-AssemblyLoaded (Join-Path -Path $assemblyLoadPath -ChildPath "Kestrun.dll")

# 1️⃣  Load & register your DLL folders (as before):
[Kestrun.Utilities.AssemblyAutoLoader]::PreloadAll($false, @($assemblyLoadPath))

# 2️⃣  When the runspace or script is finished:
[Kestrun.Utilities.AssemblyAutoLoader]::Clear($true)   # remove hook + folders

# 3️⃣  Load private functions
Get-ChildItem "$($moduleRootPath)/Private/*.ps1" -Recurse | ForEach-Object { . ([System.IO.Path]::GetFullPath($_)) }

# only import public functions
$sysfuncs = Get-ChildItem Function:

# only import public alias
$sysaliases = Get-ChildItem Alias:

# load public functions
Get-ChildItem "$($moduleRootPath)/Public/*.ps1" -Recurse | ForEach-Object { . ([System.IO.Path]::GetFullPath($_)) }

# get functions from memory and compare to existing to find new functions added
$funcs = Get-ChildItem Function: | Where-Object { $sysfuncs -notcontains $_ }
$aliases = Get-ChildItem Alias: | Where-Object { $sysaliases -notcontains $_ }
# export the module's public functions
if ($funcs) {
    if ($aliases) {
        Export-ModuleMember -Function ($funcs.Name) -Alias $aliases.Name
    }
    else {
        Export-ModuleMember -Function ($funcs.Name)
    }
}

if ([Kestrun.KestrunHostManager]::KestrunRoot -ne $script:KestrunRoot) {
    # Set the Kestrun root path for the host manager
    [Kestrun.KestrunHostManager]::KestrunRoot = $script:KestrunRoot
}
[Kestrun.KestrunHostManager]::VariableBaseline = Get-Variable | Select-Object -ExpandProperty Name
# Ensure that the Kestrun host manager is destroyed to clean up resources.