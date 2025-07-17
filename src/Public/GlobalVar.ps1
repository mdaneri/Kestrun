# GlobalVars.psm1

function Set-KrGlobalVar {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]
        [string]$Name,

        [Parameter(Mandatory)]
        [object]$Value,

        [switch]$ReadOnly
    )

    # Define or update the variable; throws if it was already read-only
    $ok = [KestrumLib.GlobalVariables]::Define(
        $Name,
        $Value,
        [bool]$ReadOnly.IsPresent
    )
    if (-not $ok) {
        throw "Failed to set global variable '$Name' (read-only or name conflict)."
    }
}

function Get-KrGlobalVar {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory, ValueFromPipeline)]
        [string]$Name
    )

    # Retrieve (or $null if not defined)
    [KestrumLib.GlobalVariables]::Get($Name)
}

function Remove-KrGlobalVar {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]
        [string]$Name
    )

    # Remove only if not read-only
    $ok = [KestrumLib.GlobalVariables]::Remove($Name)
    if (-not $ok) {
        throw "Failed to remove global variable '$Name' (it may be read-only or not exist)."
    }
}
