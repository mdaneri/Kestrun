function Add-KrPowerShellRazorPagesRuntime {
    <#
.SYNOPSIS
   Adds PowerShell support for Razor Pages.
.DESCRIPTION
    This cmdlet allows you to register Razor Pages with PowerShell support in the Kestrun server.
    It can be used to serve dynamic web pages using Razor syntax with PowerShell code blocks.
.PARAMETER Server
    The Kestrun server instance to which the PowerShell Razor Pages service will be added.
.PARAMETER PathPrefix
    An optional path prefix for the Razor Pages. If specified, the Razor Pages will be served under this path.
.EXAMPLE
    $server | Add-KrPowerShellRazorPagesRuntime -PathPrefix '/pages'
    This example adds PowerShell support for Razor Pages to the server, with a path prefix of '/pages'.
.EXAMPLE
    $server | Add-KrPowerShellRazorPagesRuntime
    This example adds PowerShell support for Razor Pages to the server without a path prefix.
.NOTES
    This cmdlet is used to register Razor Pages with PowerShell support in the Kestrun server, allowing you to serve dynamic web pages using Razor syntax with PowerShell code blocks.
#>
    [CmdletBinding()]
    [OutputType([Kestrun.KestrunHost])]
    param(
        [Parameter(Mandatory = $false, ValueFromPipeline = $true)]
        [Kestrun.KestrunHost]$Server,

        [Parameter()]
        [string]$PathPrefix,

        [Parameter()]
        [switch]$PassThru
    )
    process {
        if ([string]::IsNullOrWhiteSpace($PathPrefix)) {
            $Server.AddPowerShellRazorPages() | Out-Null
        }
        else {
            $Server.AddPowerShellRazorPages([Microsoft.AspNetCore.Http.PathString]::new($PathPrefix)) | Out-Null
        }

        if ($PassThru.IsPresent) {
            # if the PassThru switch is specified, return the server instance
            # Return the modified server instance
            return $Server
        }

    }
}