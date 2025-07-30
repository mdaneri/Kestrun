
<#
.SYNOPSIS
    Creates a new Kestrun server instance.
.DESCRIPTION
    This function initializes a new Kestrun server instance with the specified name and logger.
.PARAMETER Name
    The name of the Kestrun server instance to create.
.PARAMETER Logger
    An optional Serilog logger instance to use for logging.
.EXAMPLE
    New-KrServer -Name "MyKestrunServer"
    Creates a new Kestrun server instance with the specified name.
.NOTES
    This function is designed to be used in the context of a Kestrun server setup.
#>
function New-KrServer {
    [CmdletBinding(SupportsShouldProcess = $true)]
    [OutputType([Kestrun.Hosting.KestrunHost])]
    param(
        [Parameter(Mandatory = $true, ValueFromPipeline = $true)]
        [string]$Name,
        [Parameter()]
        [Serilog.ILogger]$Logger = [Serilog.Log]::Logger,
        [Parameter()]
        [switch]$PassThru
    )
    process {
        $loadedModules = Get-UserImportedModule
        $modulePaths = @($loadedModules | ForEach-Object { $_.Path })
        if ($PSCmdlet.ShouldProcess("Kestrun server '$Name'", "Create new server instance")) { 
            $server = [Kestrun.KestrunHostManager]::Create($Name, $Logger, [string[]] $modulePaths) 
            if ($PassThru.IsPresent) {
                # if the PassThru switch is specified, return the server instance
                # Return the modified server instance
                return $Server
            }

        }
    }
}
