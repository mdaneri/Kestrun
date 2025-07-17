# GlobalVars.psm1

<#
.SYNOPSIS
    Defines or updates a global variable accessible across Kestrun scripts.

.DESCRIPTION
    Stores a value in the Kestrun global variable table. Variables may be marked
    as read-only to prevent accidental modification.
.PARAMETER Name
    Name of the variable to create or update.
.PARAMETER Value
    Value to assign to the variable.
.PARAMETER ReadOnly
    If specified, the variable is created as read-only and cannot be changed
    later.
#>
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

<#
.SYNOPSIS
    Retrieves the value of a previously defined global variable.

.DESCRIPTION
    Looks up a variable in the Kestrun global variable table and returns its
    value. If the variable does not exist, `$null` is returned.
.PARAMETER Name
    Name of the variable to retrieve.
#>
function Get-KrGlobalVar {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory, ValueFromPipeline)]
        [string]$Name
    )

    # Retrieve (or $null if not defined)
    [KestrumLib.GlobalVariables]::Get($Name)
}

<#
.SYNOPSIS
    Removes a global variable from the Kestrun variable table.

.DESCRIPTION
    Deletes the specified variable if it exists and is not marked as read-only.
.PARAMETER Name
    Name of the variable to remove.
#>
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
