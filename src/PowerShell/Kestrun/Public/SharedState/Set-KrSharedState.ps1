function Set-KrSharedState {
    <#
    .SYNOPSIS
        Defines or updates a global variable accessible across Kestrun scripts.

    .DESCRIPTION
        Stores a value in the Kestrun global variable table. Variables may be marked
        as read-only to prevent accidental modification.
        If the variable already exists, its value is updated. If it does not exist,
        it is created. 
    .PARAMETER Name
        Name of the variable to create or update.
    .PARAMETER Value
        Value to assign to the variable.
    #>
    [KestrunRuntimeApi([KestrunApiContext]::Definition)]
    [CmdletBinding(SupportsShouldProcess = $true)]
    [OutputType([Kestrun.Hosting.KestrunHost])]
    param(
        [Parameter(Mandatory)]
        [string]$Name,

        [Parameter(Mandatory)]
        [object]$Value
    )
    process {
        if ($PSCmdlet.ShouldProcess("Kestrun shared variable '$Name'", "Set")) {
            # Define or update the variable; throws if it was already read-only
            $null = [Kestrun.SharedState.SharedStateStore]::Set(
                $Name,
                $Value
            )
        }
    }
}