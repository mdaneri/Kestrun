
<#
.SYNOPSIS
    Enables Kestrun server configuration and starts the server.
.DESCRIPTION
    This function applies the configuration to the Kestrun server and starts it.
.PARAMETER Server
    The Kestrun server instance to configure and start. This parameter is mandatory.
.PARAMETER Quiet
    If specified, suppresses output messages during the configuration and startup process.
.EXAMPLE
    Enable-KrConfiguration -Server $server
    Applies the configuration to the specified Kestrun server instance and starts it.
.NOTES
    This function is designed to be used after the server has been configured with routes, listeners,
#>
function Enable-KrConfiguration {
    [CmdletBinding()]
    [OutputType([Kestrun.KestrunHost])]
    [Diagnostics.CodeAnalysis.SuppressMessageAttribute('PSAvoidUsingWriteHost', '')]
    param(
        [Parameter(Mandatory = $false, ValueFromPipeline = $true)]
        [Kestrun.KestrunHost]$Server,
        [Parameter()]
        [switch]$Quiet
    )
    process {
        # Ensure the server instance is resolved
        $Server = Resolve-KestrunServer -Server $Server
        $Server.EnableConfiguration() | Out-Null
        if (-not $Quiet.IsPresent) {
            Write-Host "Kestrun server configuration enabled successfully."
            Write-Host "Server Name: $($Server.Options.ApplicationName)"
        }
        return $Server
    }
}
