function Add-KrApiKeyAuthentication {
    <#
    .SYNOPSIS
        Adds API key authentication to the Kestrun server.
    .DESCRIPTION
        Configures the Kestrun server to use API key authentication for incoming requests.
    .PARAMETER Server
        The Kestrun server instance to configure.
    .PARAMETER Name
        The name of the API key authentication scheme.
    .PARAMETER Options
        The options to configure the API key authentication.
    .PARAMETER ScriptBlock
        A script block that contains the logic for validating the API key.
    .PARAMETER CsCode
        C# code that contains the logic for validating the API key.
    .PARAMETER CodeFilePath
        Path to a file containing C# code that contains the logic for validating the API key.
    .PARAMETER ExpectedKey
        The expected API key to validate against.
    .PARAMETER HeaderName
        The name of the header to look for the API key.
    .PARAMETER AdditionalHeaderNames
        Additional headers to check for the API key.
    .PARAMETER AllowQueryStringFallback
        If specified, allows the API key to be provided in the query string.
    .PARAMETER AllowInsecureHttp
        If specified, allows the API key to be provided over HTTP instead of HTTPS.
    .PARAMETER EmitChallengeHeader
        If specified, emits a challenge header when the API key is missing or invalid.
    .PARAMETER ChallengeHeaderFormat
        The format of the challenge header to emit.
    .PARAMETER Logger
        A logger to use for logging authentication events.
    .PARAMETER PassThru
        If specified, returns the modified server instance after adding the authentication.
    .EXAMPLE
        Add-KrApiKeyAuthentication -Name 'MyApiKey' -ExpectedKey '12345' -HeaderName 'X-Api-Key'
        This example adds API key authentication to the server with the specified expected key and header name.
    .EXAMPLE
        Add-KrApiKeyAuthentication -Name 'MyApiKey' -ScriptBlock {
            param($username, $password)
            return $username -eq 'admin' -and $password -eq 'password'
        }
        This example adds API key authentication using a script block to validate the API key.
    .EXAMPLE
        Add-KrApiKeyAuthentication -Name 'MyApiKey' -CsCode @"
            return username == "admin" && password == "password";
        "@
        This example adds API key authentication using C# code to validate the API key.
    .EXAMPLE
        Add-KrApiKeyAuthentication -Name 'MyApiKey' -CodeFilePath 'C:\path\to\code.cs'
        This example adds API key authentication using a C# code file to validate the API key.
    .LINK
        https://learn.microsoft.com/en-us/dotnet/api/microsoft.aspnetcore.authentication.apikey.apikeyauthenticationoptions?view=aspnetcore-8.0
    .NOTES
        This cmdlet is used to configure API key authentication for the Kestrun server, allowing you to secure your APIs with API keys. 
    #>
    [CmdletBinding(defaultParameterSetName = 'ItemsScriptBlock')]
    [OutputType([Kestrun.Hosting.KestrunHost])]
    param(
        [Parameter(Mandatory = $false, ValueFromPipeline)]
        [Kestrun.Hosting.KestrunHost]$Server,

        [Parameter(Mandatory = $true)]
        [string]$Name,

        [Parameter(Mandatory = $true, ParameterSetName = 'Options')]
        [Kestrun.Authentication.ApiKeyAuthenticationOptions]$Options,

        [Parameter(Mandatory = $true, ParameterSetName = 'ItemsScriptBlock')]
        [scriptblock]$ScriptBlock,

        [Parameter(Mandatory = $true, ParameterSetName = 'ItemsCsCode')]
        [string]$CsCode,

        [Parameter(Mandatory = $true, ParameterSetName = 'ItemsCodeFilePath')]
        [string]$CodeFilePath,

        [Parameter(Mandatory = $true, ParameterSetName = 'ItemsExpectedKey')]
        [string]$ExpectedKey,

        [Parameter(ParameterSetName = 'ItemsCodeFilePath')]
        [Parameter(ParameterSetName = 'ItemsScriptBlock')]
        [Parameter(ParameterSetName = 'ItemsCsCode')]
        [Parameter(ParameterSetName = 'ItemsExpectedKey')]
        [string]$HeaderName,

        [Parameter(ParameterSetName = 'ItemsCodeFilePath')]
        [Parameter(ParameterSetName = 'ItemsScriptBlock')]
        [Parameter(ParameterSetName = 'ItemsCsCode')]
        [Parameter(ParameterSetName = 'ItemsExpectedKey')]
        [string[]]$AdditionalHeaderNames,

        [Parameter(ParameterSetName = 'ItemsCodeFilePath')]
        [Parameter(ParameterSetName = 'ItemsScriptBlock')]
        [Parameter(ParameterSetName = 'ItemsCsCode')]
        [Parameter(ParameterSetName = 'ItemsExpectedKey')]
        [switch]$AllowQueryStringFallback,

        [Parameter(ParameterSetName = 'ItemsCodeFilePath')]
        [Parameter(ParameterSetName = 'ItemsScriptBlock')]
        [Parameter(ParameterSetName = 'ItemsCsCode')]
        [Parameter(ParameterSetName = 'ItemsExpectedKey')]
        [Serilog.ILogger]$Logger,


        [Parameter(ParameterSetName = 'ItemsCodeFilePath')]
        [Parameter(ParameterSetName = 'ItemsScriptBlock')]
        [Parameter(ParameterSetName = 'ItemsCsCode')]
        [Parameter(ParameterSetName = 'ItemsExpectedKey')]
        [switch]$AllowInsecureHttp,

        [Parameter(ParameterSetName = 'ItemsCodeFilePath')]
        [Parameter(ParameterSetName = 'ItemsScriptBlock')]
        [Parameter(ParameterSetName = 'ItemsCsCode')]
        [Parameter(ParameterSetName = 'ItemsExpectedKey')]
        [switch]$EmitChallengeHeader,

        [Parameter(ParameterSetName = 'ItemsCodeFilePath')]
        [Parameter(ParameterSetName = 'ItemsScriptBlock')]
        [Parameter(ParameterSetName = 'ItemsCsCode')]
        [Parameter(ParameterSetName = 'ItemsExpectedKey')]
        [Kestrun.Authentication.ApiKeyChallengeFormat]$ChallengeHeaderFormat,
 
        [Parameter()]
        [switch]$PassThru
    )
    process {
        if ($PSCmdlet.ParameterSetName -ne 'Options') {
            $Options = [Kestrun.Authentication.ApiKeyAuthenticationOptions]::new()
            $Options.CodeSettings = [Kestrun.Authentication.AuthenticationCodeSettings]::new()
            if (-not [string]::IsNullOrWhiteSpace($ExpectedKey)) {
                $Options.ExpectedKey = $ExpectedKey
            }
            elseif ($null -ne $ScriptBlock) {
                $Options.CodeSettings.Code = $ScriptBlock.ToString()
                $Options.CodeSettings.Language = [Kestrun.Scripting.ScriptLanguage]::PowerShell

            }
            elseif (-not [string]::IsNullOrWhiteSpace($CsCode)) {
                $Options.CodeSettings.Code = $CsCode
                $Options.CodeSettings.Language = [Kestrun.Scripting.ScriptLanguage]::CSharp
            }
            elseif (-not [string]::IsNullOrWhiteSpace($CodeFilePath)) {
                if (-not (Test-Path -Path $CodeFilePath)) {
                    throw "The specified code file path does not exist: $CodeFilePath"
                }
                $extension = Split-Path -Path $CodeFilePath -Extension
                switch ($extension) {
                    ".ps1" {
                        $Options.CodeSettings.Language = [Kestrun.Scripting.ScriptLanguage]::PowerShell
                    }
                    ".cs" {
                        $Options.CodeSettings.Language = [Kestrun.Scripting.ScriptLanguage]::CSharp
                    }
                    Default {
                        throw "Unsupported code file extension. Only .ps1 and .cs files are supported."
                    }
                }
                $Options.CodeSettings.Code = Get-Content -Path $CodeFilePath -Raw
            }

            if (-not [string]::IsNullOrWhiteSpace($HeaderName)) {
                $Options.HeaderName = $HeaderName
            }
            if ($AdditionalHeaderNames.Count -gt 0) {
                $Options.AdditionalHeaderNames = $AdditionalHeaderNames
            }
            if ($AllowQueryStringFallback.IsPresent) {
                $Options.AllowQueryStringFallback = $AllowQueryStringFallback.IsPresent
            } 
            if ($EmitChallengeHeader.IsPresent) {
                $Options.EmitChallengeHeader = $EmitChallengeHeader.IsPresent
            }
            if ($null -ne $ChallengeHeaderFormat) {
                $Options.ChallengeHeaderFormat = $ChallengeHeaderFormat
            }

            if ($AllowInsecureHttp.IsPresent) {
                $Options.RequireHttps = $false
            }
            else {
                $Options.RequireHttps = $true
            }
            if ($null -ne $Logger) {
                $Options.Logger = $Logger
            }
        }
        # Ensure the server instance is resolved
        $Server = Resolve-KestrunServer -Server $Server

        [Kestrun.Hosting.KestrunHostAuthExtensions]::AddApiKeyAuthentication(
            $Server,
            $Name,
            $Options
        ) | Out-Null
        if ($PassThru.IsPresent) {
            # if the PassThru switch is specified, return the server instance
            # Return the modified server instance
            return $Server
        }
    }
}