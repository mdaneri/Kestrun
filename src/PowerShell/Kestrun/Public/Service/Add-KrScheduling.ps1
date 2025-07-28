function Add-KrScheduling {
    <#
    .SYNOPSIS
        Adds scheduling support to the Kestrun server.
    .DESCRIPTION
        This cmdlet allows you to register a scheduling service with the Kestrun server.
        It can be used to manage scheduled tasks and jobs in the context of the Kestrun server.
    .PARAMETER Server
        The Kestrun server instance to which the scheduling service will be added.
    .PARAMETER MaxRunspaces
        The maximum number of runspaces to use for scheduling tasks. If not specified, defaults to 0 (unlimited).
    .EXAMPLE
        $server | Add-KrScheduling -MaxRunspaces 5
        This example adds scheduling support to the server, with a maximum of 5 runspaces.
    .EXAMPLE
        $server | Add-KrScheduling
        This example adds scheduling support to the server without specifying a maximum number of runspaces.
    .NOTES
        This cmdlet is used to register a scheduling service with the Kestrun server, allowing you to manage scheduled tasks and jobs.
    #>
    [CmdletBinding()]
    [OutputType([Kestrun.KestrunHost])]
    param(
        [Parameter(Mandatory = $false, ValueFromPipeline = $true)]
        [Kestrun.KestrunHost]$Server,
        [Parameter()]
        [int]$MaxRunspaces
    )
    process {
        # Ensure the server instance is resolved
        $Server = Resolve-KestrunServer -Server $Server
        if ($MaxRunspaces -eq 0) {
            $Server.AddScheduling() | Out-Null
        }
        else {
            $Server.AddScheduling($MaxRunspaces) | Out-Null
        }

        # Return the modified server instance
        return $Server
    }
}
