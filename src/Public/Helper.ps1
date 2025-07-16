<#
.SYNOPSIS
    Selects which CPython runtime pythonnet will embed.

.DESCRIPTION
    • With no -Path the newest 64-bit CPython is auto-discovered.
    • If the env-var PYTHONNET_PYDLL is already present the function
      exits immediately—unless you pass -Force (or give an explicit -Path).
    • Applies the setting both to the environment and to the live
      [Python.Runtime.Runtime]::PythonDLL property when possible.

.PARAMETER Path
    Full path to pythonXY.dll / libpythonX.Y.so / libpythonX.Y.dylib.

.PARAMETER Force
    Override an existing PYTHONNET_PYDLL environment variable.

.EXAMPLE
    # Leave current setting untouched if already configured
    Set-KrPythonRuntime

.EXAMPLE
    # Override whatever is set and pin CPython 3.12
    Set-KrPythonRuntime -Path '/opt/python312/lib/libpython3.12.so' -Force
#>
function Set-KrPythonRuntime {
    [CmdletBinding()]
    param(
        [string] $Path,
        [switch] $Force
    )

    # ------------------------------------------------------------
    # 0. Does pythonnet already know a valid DLL / .so / .dylib?
    # ------------------------------------------------------------
    $currentDll = [Python.Runtime.Runtime]::PythonDLL

    if (-not $Force -and -not $Path -and
        $currentDll -and (Test-Path $currentDll)) {

        Write-Verbose "pythonnet already configured → $currentDll"
        return (Resolve-Path $currentDll).Path
    }

    # ------------------------------------------------------------
    # 1. If caller didn’t supply -Path, auto-discover
    # ------------------------------------------------------------
    if (-not $Path) {
        if ($IsWindows) {
            # Windows: take the DLL next to the first python.exe on PATH
            $pyExe = (Get-Command python.exe, python3.exe -ErrorAction Ignore |
                Select-Object -First 1).Source
            if ($pyExe) {
                $Path = Get-ChildItem (Join-Path (Split-Path $pyExe) 'python*.dll') -ErrorAction Ignore |
                Sort-Object VersionInfo.FileVersion -Descending |
                Select-Object -First 1 -Expand FullName
            }
        }
        else {
            # Linux / macOS: ask whereis for libpython3*.so / .dylib
            $pattern = if ($IsMacOS) { 'libpython3*.dylib' } else { 'libpython3*.so' }
            $Path = & whereis -b $pattern 2>$null |
            Select-String -Pattern $pattern |
            ForEach-Object { $_.ToString().Split(' ', 2)[1] } |
            Sort-Object Length | Select-Object -First 1
        }
    }

    if (-not $Path -or -not (Test-Path $Path)) {
        throw "Could not locate a CPython runtime. Install Python ≥3.11 or supply -Path (-Force overrides existing setting)."
    }

    # ------------------------------------------------------------
    # 2. Tell pythonnet to use this runtime
    # ------------------------------------------------------------
    $Path = (Resolve-Path $Path).Path
    Write-Verbose "pythonnet will use: $Path"

    # If pythonnet already loaded: update in-process
    [Python.Runtime.Runtime]::PythonDLL = $Path

    return $Path
}

<#>
function Resolve-KrPath {
    param (
        [string] $Path,
        [switch] $Force
    )
    Write-KrLog -level "Verbose" -Message "Resolve-KrPath : Relative Path :$($script:KestrunRoot)"
    $resolved = (Resolve-Path -Path $Path -RelativeBasePath $script:KestrunRoot -ErrorAction SilentlyContinue)
    if ($null -eq $resolved) {
        return $Path
    }
    return $resolved.Path 
}#>
 

function Resolve-KrPath {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory, Position = 0, ValueFromPipeline)]
        [string] $Path,

        [Parameter(parameterSetName = 'RelativeBasePath')]
        [string] $RelativeBasePath,

        [Parameter(parameterSetName = 'KestrunRoot')]
        [switch] $KestrunRoot,

        [Parameter()]
        [switch] $Test
    )
    process {
        # --- 1. Expand ~/env in both Path and, if supplied, RelativeBasePath ---
        $expand = {
            param($p)
            if ($p -like '~*') {
                $p = $p -replace '^~', $HOME
            }
            [Environment]::ExpandEnvironmentVariables($p)
        }

        $p3 = & $expand $Path

        if($KestrunRoot) {
            # Use the Kestrun root as base
            $RelativeBasePath = $script:KestrunRoot
        }

        if ($RelativeBasePath) {
            # Expand + normalize the base, even if it doesn't exist
            $base3 = & $expand $RelativeBasePath
            $baseFull = [IO.Path]::GetFullPath($base3)

            # If $Path is rooted, ignore the base; else combine
            if ([IO.Path]::IsPathRooted($p3)) {
                $full = [IO.Path]::GetFullPath($p3)
            }
            else {
                $combined = [IO.Path]::Combine($baseFull, $p3)
                $full = [IO.Path]::GetFullPath($combined)
            }
        }
        else {
            # No base supplied: just make absolute against current directory
            $full = [IO.Path]::GetFullPath($p3)
        }

        # --- 4. If -Test was used and file doesn't exist, return the original input Path ---
        if ($Test -and -not (Test-Path $full)) {
            return $Path
        }

        return $full
    }
}


 


function Get-KestrunRoot {
    return $script:KestrunRoot
}