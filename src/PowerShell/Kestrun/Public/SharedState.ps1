function Set-KrSharedState {
    <#
.SYNOPSIS
    Defines or updates a global variable accessible across Kestrun scripts.

.DESCRIPTION
    Stores a value in the Kestrun global variable table. Variables may be marked
    as read-only to prevent accidental modification.
    If the variable already exists, its value is updated. If it does not exist,
    it is created.
.PARAMETER Server
    The Kestrun host instance to use for storing the variable.
    This is typically the instance running the Kestrun server.
.PARAMETER Name
    Name of the variable to create or update.
.PARAMETER Value
    Value to assign to the variable.
#>

    [CmdletBinding(SupportsShouldProcess = $true)]
    [OutputType([Kestrun.KestrunHost])]
    param(
        [Parameter(Mandatory = $true, ValueFromPipeline = $true)]
        [Kestrun.KestrunHost]$Server,

        [Parameter(Mandatory)]
        [string]$Name,

        [Parameter(Mandatory)]
        [object]$Value
    )
    process {
        if ($PSCmdlet.ShouldProcess("Kestrun shared variable '$Name'", "Set")) {
            # Define or update the variable; throws if it was already read-only
            $null = $Server.SharedState.Set(
                $Name,
                $Value
            )
        }
        # Return the server instance for chaining
        # This allows for fluent API usage
        return $Server
    }
}
function Get-KrSharedState {
    <#
.SYNOPSIS
    Retrieves the value of a previously defined global variable.

.DESCRIPTION
    Looks up a variable in the Kestrun global variable table and returns its
    value. If the variable does not exist, `$null` is returned.

.PARAMETER Server
    The Kestrun host instance to use for storing the variable.
    This is typically the instance running the Kestrun server.

.PARAMETER Name
    Name of the variable to retrieve.
#>
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true, ValueFromPipeline = $true)]
        [Kestrun.KestrunHost]$Server,
        [Parameter(Mandatory)]
        [string]$Name
    )
    process {
        # Retrieve (or $null if not defined)
        return $Server.SharedState.Get($Name)
    }
}

function Remove-KrSharedState {
    <#
.SYNOPSIS
    Removes a global variable from the Kestrun variable table.

.DESCRIPTION
    Deletes the specified variable if it exists and is not marked as read-only.

.PARAMETER Server
    The Kestrun host instance to use for storing the variable.
    This is typically the instance running the Kestrun server.

.PARAMETER Name
    Name of the variable to remove.
#>

    [CmdletBinding(SupportsShouldProcess = $true)]
    param(
        [Parameter(Mandatory = $true, ValueFromPipeline = $true)]
        [Kestrun.KestrunHost]$Server,

        [Parameter(Mandatory)]
        [string]$Name
    )
    process {
        # Remove the variable if it exists
        if ($PSCmdlet.ShouldProcess("Kestrun shared variable '$Name'", "Remove")) {
            $null = $Server.SharedState.Remove($Name)
        }
    }

}
