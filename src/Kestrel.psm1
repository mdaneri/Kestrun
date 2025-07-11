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
    param (
        [Parameter()]
        [ValidateSet("net8", "net9", "net10")]
        [string]$Version = "net8"
    )
    $versionNumber = $Version.Substring(3)
    $path = Split-Path -Path (Get-Command -Name "dotnet.exe").Source -Parent
    if (-not $path) {
        throw "Could not determine the path to the dotnet executable."
    }
    $baseDir = Join-Path -Path $path -ChildPath "shared" -AdditionalChildPath "Microsoft.AspNetCore.App"
    if (Test-Path -Path $baseDir -PathType Container) {
        $versionDirs = Get-ChildItem -Path $baseDir -Directory | Where-Object { $_.Name -like "$($versionNumber).*" } | Sort-Object Name -Descending
        foreach ($verDir in $versionDirs) {
            $assemblyPath = Join-Path -Path $verDir.FullName -ChildPath "Microsoft.AspNetCore.dll"
            if (Test-Path -Path $assemblyPath) {
                Assert-AssemblyLoaded -AssemblyPath $assemblyPath
                return
            }
        }
       
    }
    throw "Microsoft.AspNetCore.App version $Version.* not found in PSModulePath."
}

# root path
$root = Split-Path -Parent -Path $MyInvocation.MyCommand.Path 
# Usage
Add-AspNetCoreType -Version "net8"
# Add-AspNetCoreType -Version "net8.0.*"

# root path
$root = Split-Path -Parent -Path $MyInvocation.MyCommand.Path 
# Assert that the assembly is loaded
Assert-AssemblyLoaded "$root\src\Kestrel\bin\Debug\net8.0\Kestrel.dll"


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