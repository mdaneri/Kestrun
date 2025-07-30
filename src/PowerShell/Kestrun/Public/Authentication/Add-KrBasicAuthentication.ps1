function Add-KrBasicAuthentication {
    <#
    .SYNOPSIS
        Adds basic authentication to the Kestrun server.
    .DESCRIPTION
        Configures the Kestrun server to use basic authentication for incoming requests.
    .PARAMETER Server
        The Kestrun server instance to configure.
    #>
    [CmdletBinding(defaultParameterSetName = 'ItemsScriptBlock')]
    [OutputType([Kestrun.Hosting.KestrunHost])]
    param(
        [Parameter(Mandatory = $false, ValueFromPipeline)]
        [Kestrun.Hosting.KestrunHost]$Server,

        [Parameter(Mandatory = $true)]
        [string]$Name,

        [Parameter(Mandatory = $true, ParameterSetName = 'Options')]
        [Kestrun.Authentication.BasicAuthenticationOptions]$Options,

        [Parameter(Mandatory = $true, ParameterSetName = 'ItemsScriptBlock')]
        [scriptblock]$ScriptBlock,

        [Parameter(Mandatory = $true, ParameterSetName = 'ItemsCsCode')]
        [string]$CsCode,

        [Parameter(Mandatory = $true, ParameterSetName = 'ItemsCodeFilePath')]
        [string]$CodeFilePath,

        [Parameter(ParameterSetName = 'ItemsCodeFilePath')]
        [Parameter(ParameterSetName = 'ItemsScriptBlock')]
        [Parameter(ParameterSetName = 'ItemsCsCode')]
        [string]$HeaderName,

        [Parameter(ParameterSetName = 'ItemsCodeFilePath')]
        [Parameter(ParameterSetName = 'ItemsScriptBlock')]
        [Parameter(ParameterSetName = 'ItemsCsCode')]
        [switch]$Base64Encoded,

        [Parameter(ParameterSetName = 'ItemsCodeFilePath')]
        [Parameter(ParameterSetName = 'ItemsScriptBlock')]
        [Parameter(ParameterSetName = 'ItemsCsCode')]
        [switch]$SuppressWwwAuthenticate,

        [Parameter(ParameterSetName = 'ItemsCodeFilePath')]
        [Parameter(ParameterSetName = 'ItemsScriptBlock')]
        [Parameter(ParameterSetName = 'ItemsCsCode')]
        [Regex]$SeparatorRegex,

        [Parameter(ParameterSetName = 'ItemsCodeFilePath')]
        [Parameter(ParameterSetName = 'ItemsScriptBlock')]
        [Parameter(ParameterSetName = 'ItemsCsCode')]
        [string]$Realm,

        [Parameter(ParameterSetName = 'ItemsCodeFilePath')]
        [Parameter(ParameterSetName = 'ItemsScriptBlock')]
        [Parameter(ParameterSetName = 'ItemsCsCode')]
        [switch]$AllowInsecureHttp,

        [Parameter(ParameterSetName = 'ItemsCodeFilePath')]
        [Parameter(ParameterSetName = 'ItemsScriptBlock')]
        [Parameter(ParameterSetName = 'ItemsCsCode')]
        [Serilog.ILogger]$Logger,
 
        [Parameter()]
        [switch]$PassThru
    )
    process {
        if ($PSCmdlet.ParameterSetName -ne 'Options') {
            $options = [Kestrun.Authentication.BasicAuthenticationOptions]::new()
            $options.CodeSettings = [Kestrun.Authentication.AuthenticationCodeSettings]::new()
            if ($null -ne $ScriptBlock) {
                $options.CodeSettings.Code = $ScriptBlock.ToString()
                $options.CodeSettings.Language = [Kestrun.Scripting.ScriptLanguage]::PowerShell

            }
            elseif (-not [string]::IsNullOrWhiteSpace($CsCode)) {
                $options.CodeSettings.Code = $CsCode
                $options.CodeSettings.Language = [Kestrun.Scripting.ScriptLanguage]::CSharp
            }
            elseif (-not [string]::IsNullOrWhiteSpace($CodeFilePath)) {
                if (-not (Test-Path -Path $CodeFilePath)) {
                    throw "The specified code file path does not exist: $CodeFilePath"
                }
                $extension = Split-Path -Path $CodeFilePath -Extension
                switch ($extension) {
                    ".ps1" {
                        $options.CodeSettings.Language = [Kestrun.Scripting.ScriptLanguage]::PowerShell
                    }
                    ".cs" {
                        $options.CodeSettings.Language = [Kestrun.Scripting.ScriptLanguage]::CSharp
                    }
                    Default {
                        throw "Unsupported code file extension. Only .ps1 and .cs files are supported."
                    }
                }
                $options.CodeSettings.Code = Get-Content -Path $CodeFilePath -Raw
            }

            if (-not [string]::IsNullOrWhiteSpace($HeaderName)) {
                $options.HeaderName = $HeaderName
            }
            if ($Base64Encoded.IsPresent) {
                $options.Base64Encoded = $Base64Encoded.IsPresent
            }
            if ($SuppressWwwAuthenticate.IsPresent) {
                $options.SuppressWwwAuthenticate = $SuppressWwwAuthenticate.IsPresent
            }
            if ($null -ne $SeparatorRegex) {
                $options.SeparatorRegex = $SeparatorRegex
            }
            if (-not [string]::IsNullOrWhiteSpace($Realm)) {
                $options.Realm = $Realm
            }
            if ($AllowInsecureHttp.IsPresent) {
                $options.RequireHttps = $false
            }else {
                $options.RequireHttps = $true
            }
            if ($null -ne $Logger) {
                $options.Logger = $Logger
            }
        }
        # Ensure the server instance is resolved
        $Server = Resolve-KestrunServer -Server $Server

        [Kestrun.Hosting.KestrunHostAuthExtensions]::AddBasicAuthentication(
            $Server,
            $Name,
            $options
        )
        if ($PassThru.IsPresent) {
            # if the PassThru switch is specified, return the server instance
            # Return the modified server instance
            return $Server
        }
    }
}