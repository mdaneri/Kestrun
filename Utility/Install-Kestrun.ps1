
param(
    [Parameter()]
    [string]$FileVersion = "./version.json",
    [Parameter()]
    [switch]$Remove
)

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



$PSPaths = if ($IsWindows) {
    $env:PSModulePath -split ';'
}
else {
    $env:PSModulePath -split ':'
}
$Version=Get-Version -FileVersion $FileVersion

$dest = Join-Path -Path $PSPaths[0] -ChildPath 'Kestrun' -AdditionalChildPath "$Version"
if ($Remove) {
    if (!(Test-Path $dest)) {
        Write-Warning "Directory $dest doesn't exist"
    }

    Write-Host "Deleting module from $dest"
    Remove-Item -Path $dest -Recurse -Force | Out-Null
    return
}

if (Test-Path $dest) {
    if ($Force) {
        Remove-Item -Path $dest -Recurse -Force | Out-Null
    }
    else {
        Write-Warning "Directory $dest already exists. Use -Force to overwrite."
        return
    }
}

# create the dest dir
New-Item -Path $dest -ItemType Directory -Force | Out-Null
$path = './src/PowerShell/Kestrun/*'

 
Copy-Item -Path (Join-Path -Path $path -ChildPath $_) -Destination $dest -Force -Recurse | Out-Null
 
 

Write-Host "Deployed to $dest"


