function Add-KrBasicAuthentication {
    <#
    .SYNOPSIS
        Adds basic authentication to the Kestrun server.
    .DESCRIPTION
        Configures the Kestrun server to use basic authentication for incoming requests.
    .PARAMETER Server
        The Kestrun server instance to configure.
    #>
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $false, ValueFromPipeline)]
        [Kestrun.Hosting.KestrunHost]$Server,
        [Parameter(Mandatory = $true)]
        [string]$Name,
        [parameter()]
        [Kestrun.Scripting.ScriptLanguage]$Language,

        [Parameter(Mandatory = $false)]
        [string]$Code,

        [Parameter()]
        [switch]$PassThru
    )
    process {
        $options = [Kestrun.Authentication.BasicAuthenticationOptions]::new() 
        $options.CodeSettings = [Kestrun.Authentication.AuthenticationCodeSettings]::new()
        $options.CodeSettings.Language = $Language
        $options.CodeSettings.Code = $Code
        # Ensure the server instance is resolved

        $Server = Resolve-KestrunServer -Server $Server

        [Kestrun.Hosting.KestrunHostAuthExtensions]::AddBasicAuthentication(
            $Server,
            $Name,
            $options
        )
        if ($PassThru) {
            $Server
        }
    }
}