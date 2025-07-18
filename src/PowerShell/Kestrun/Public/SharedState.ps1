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
#>
function Set-KrSharedState {
    [CmdletBinding(SupportsShouldProcess = $true)]
    param(
        [Parameter(Mandatory)]
        [string]$Name,

        [Parameter(Mandatory)]
        [object]$Value 
    )

    if ($PSCmdlet.ShouldProcess("Kestrun shared variable '$Name'", "Set")) {
        # Define or update the variable; throws if it was already read-only
        $null = [Kestrun.SharedState]::Set(
            $Name,
            $Value
        )
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
function Get-KrSharedState {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]
        [string]$Name
    )

    # Retrieve (or $null if not defined)
    return [Kestrun.SharedState]::Get($Name)
}

<#
.SYNOPSIS
    Removes a global variable from the Kestrun variable table.

.DESCRIPTION
    Deletes the specified variable if it exists and is not marked as read-only.
.PARAMETER Name
    Name of the variable to remove.
#>
function Remove-KrSharedState {
    [CmdletBinding(SupportsShouldProcess = $true)]
    param(
        [Parameter(Mandatory)]
        [string]$Name
    )

    if ($PSCmdlet.ShouldProcess("Kestrun shared variable '$Name'", "Remove")) {
        $null = [Kestrun.SharedState]::Remove($Name)
    }

}
