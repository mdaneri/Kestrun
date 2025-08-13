function Get-KrSharedState {
    <#
    .SYNOPSIS
        Retrieves the value of a previously defined global variable.

    .DESCRIPTION
        Looks up a variable in the Kestrun global variable table and returns its
        value. If the variable does not exist, `$null` is returned.

    .PARAMETER Name
        Name of the variable to retrieve.
    #>
    [KestrunRuntimeApi('Everywhere')]
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]
        [string]$Name
    )
    process {
        # Retrieve (or $null if not defined)
        return [Kestrun.SharedState.SharedStateStore]::Get($Name)
    }
}