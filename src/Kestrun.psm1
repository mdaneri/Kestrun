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
        [string]$AssemblyPath
    )
    $assemblyName = [System.Reflection.AssemblyName]::GetAssemblyName($AssemblyPath).Name
    $loaded = [AppDomain]::CurrentDomain.GetAssemblies() | Where-Object { $_.GetName().Name -eq $assemblyName }
    if (-not $loaded) {
        Add-Type -Path $AssemblyPath
    }
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
        [ValidateSet("net8", "net9", "net10")]
        [string]$Version = "net8"
    )
    $versionNumber = $Version.Substring(3)
    $dotnetPath = (Get-Command -Name "dotnet.exe" -ErrorAction Stop).Source
    $dotnetDir = Split-Path -Path $dotnetPath -Parent
    if (-not $dotnetDir) {
        throw "Could not determine the path to the dotnet executable."
    }
    $baseDir = Join-Path -Path $dotnetDir -ChildPath "shared\Microsoft.AspNetCore.App"
    if (-not (Test-Path -Path $baseDir -PathType Container)) {
        throw "ASP.NET Core shared framework not found at $baseDir."
    }
    $versionDirs = Get-ChildItem -Path $baseDir -Directory | Where-Object { $_.Name -like "$($versionNumber).*" } | Sort-Object Name -Descending
    foreach ($verDir in $versionDirs) {
        $assemblies = @(
            "Microsoft.AspNetCore.dll",
            "Microsoft.AspNetCore.ResponseCompression.dll",
            "Microsoft.AspNetCore.Http.Results.dll",
            "Microsoft.AspNetCore.StaticFiles.dll"
        )
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
$script:KestrunRoot = $MyInvocation.PSScriptRoot
# root path
$moduleRootPath = Split-Path -Parent -Path $MyInvocation.MyCommand.Path
# Usage
Add-AspNetCoreType -Version "net8"
# Add-AspNetCoreType -Version "net8.0.*"
 
# Assert that the assembly is loaded
Assert-AssemblyLoaded "$moduleRootPath\Kestrun\bin\Debug\net8.0\Kestrun.dll"
#Assert-AssemblyLoaded "$KestrunRoot \Kestrun\bin\Debug\net8.0\python.runtime.dll"
#Assert-AssemblyLoaded "$KestrunRoot \Kestrun\bin\Debug\net8.0\Microsoft.CodeAnalysis.dll"
Assert-AssemblyLoaded "$($PSHOME)\Microsoft.CodeAnalysis.dll"
# load private functions
Get-ChildItem "$($moduleRootPath)/Private/*.ps1" | ForEach-Object { . ([System.IO.Path]::GetFullPath($_)) }

# only import public functions
$sysfuncs = Get-ChildItem Function:

# only import public alias
$sysaliases = Get-ChildItem Alias:

# load public functions
Get-ChildItem "$($moduleRootPath)/Public/*.ps1" | ForEach-Object { . ([System.IO.Path]::GetFullPath($_)) }

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