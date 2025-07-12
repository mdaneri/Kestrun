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
            "Microsoft.AspNetCore.Http.Results.dll"
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

# root path
$root = Split-Path -Parent -Path $MyInvocation.MyCommand.Path 
# Usage
Add-AspNetCoreType -Version "net8"
# Add-AspNetCoreType -Version "net8.0.*"

# root path
$root = Split-Path -Parent -Path $MyInvocation.MyCommand.Path 
# Assert that the assembly is loaded
Assert-AssemblyLoaded "$root\Kestrun\bin\Debug\net8.0\Kestrun.dll"

# load private functions
Get-ChildItem "$($root)/Private/*.ps1" | ForEach-Object { . ([System.IO.Path]::GetFullPath($_)) }

# only import public functions
$sysfuncs = Get-ChildItem Function:

# only import public alias
$sysaliases = Get-ChildItem Alias:

# load public functions
Get-ChildItem "$($root)/Public/*.ps1" | ForEach-Object { . ([System.IO.Path]::GetFullPath($_)) }

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