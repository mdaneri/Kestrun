
function Set-KrServerOption {
    [CmdletBinding(SupportsShouldProcess = $true)]
    <#
.SYNOPSIS
    Configures advanced options and operational limits for a Kestrun server instance.

.DESCRIPTION
    The Set-KrServerOption function allows fine-grained configuration of a Kestrun server instance. 
    It enables administrators to control server behavior, resource usage, and protocol compliance by 
    setting limits on request sizes, connection counts, timeouts, and other operational parameters. 
    Each parameter is optional and, if not specified, the server will use its built-in default value.

.PARAMETER Server
    The Kestrun server instance to configure. This parameter is mandatory and must be a valid server object.

.PARAMETER MaxRequestBodySize
    Specifies the maximum allowed size of the HTTP request body in bytes. 
    Requests exceeding this size will be rejected. 
    Default: 30,000,000 bytes (28.6 MB).

.PARAMETER MaxConcurrentConnections
    Sets the maximum number of concurrent client connections allowed to the server. 
    Additional connection attempts will be queued or rejected. 
    Default: Unlimited (no explicit limit).

.PARAMETER MaxRequestHeaderCount
    Defines the maximum number of HTTP headers permitted in a single request. 
    Requests with more headers will be rejected. 
    Default: 100.

.PARAMETER KeepAliveTimeoutSeconds
    Specifies the duration, in seconds, that a connection is kept alive when idle before being closed. 
    Default: 120 seconds.

.PARAMETER MaxRequestBufferSize
    Sets the maximum size, in bytes, of the buffer used for reading HTTP requests. 
    Default: 1048576 bytes (1 MB).

.PARAMETER MaxRequestHeadersTotalSize
    Specifies the maximum combined size, in bytes, of all HTTP request headers. 
    Requests exceeding this size will be rejected. 
    Default: 32768 bytes (32 KB).

.PARAMETER MaxRequestLineSize
    Sets the maximum allowed length, in bytes, of the HTTP request line (method, URI, and version). 
    Default: 8192 bytes (8 KB).

.PARAMETER MaxResponseBufferSize
    Specifies the maximum size, in bytes, of the buffer used for sending HTTP responses. 
    Default: 65536 bytes (64 KB).

.PARAMETER MinRequestBodyDataRate
    Defines the minimum data rate, in bytes per second, required for receiving the request body. 
    If the rate falls below this threshold, the connection may be closed. 
    Default: 240 bytes/second.

.PARAMETER MinResponseDataRate
    Sets the minimum data rate, in bytes per second, required for sending the response. 
    Default: 240 bytes/second.

.PARAMETER RequestHeadersTimeoutSeconds
    Specifies the maximum time, in seconds, allowed to receive the complete set of request headers. 
    Default: 30 seconds.

.PARAMETER AllowSynchronousIO
    If set to $true, allows synchronous IO operations on the server. 
    Synchronous IO can impact scalability and is generally discouraged. 
    Default: $false.

.PARAMETER DisableResponseHeaderCompression
    If set to $true, disables compression of HTTP response headers. 
    Default: $false.

.PARAMETER DenyServerHeader
    If set to $true, removes the 'Server' HTTP header from responses for improved privacy and security. 
    Default: $false.

.PARAMETER AllowAlternateSchemes
    If set to $true, allows alternate URI schemes (other than HTTP/HTTPS) in requests. 
    Default: $false.

.PARAMETER AllowHostHeaderOverride
    If set to $true, permits overriding the Host header in incoming requests. 
    Default: $false.

.PARAMETER DisableStringReuse
    If set to $true, disables internal string reuse optimizations, which may increase memory usage but can help with certain debugging scenarios. 
    Default: $false.

.PARAMETER MaxRunspaces
    Specifies the maximum number of runspaces to use for script execution.
    This can help control resource usage and concurrency in script execution.
    Default: 2x CPU cores or as specified in the KestrunOptions.

.PARAMETER MinRunspaces
    Specifies the minimum number of runspaces to use for script execution.
    This ensures that at least a certain number of runspaces are always available for processing requests.
    Default: 1.

.EXAMPLE
    Set-KrServerOption -Server $srv -MaxRequestBodySize 1000000
    Configures the server instance $srv to limit request body size to 1,000,000 bytes.

.NOTES
    All parameters are optional except for -Server.
    Defaults are based on typical Kestrun server settings as of the latest release.
#>

    param(
        [Parameter(Mandatory = $true, ValueFromPipeline = $true)]
        [Kestrun.KestrunHost]$Server,
        [long]$MaxRequestBodySize , # Default is 30,000,000 
        [int]$MaxConcurrentConnections ,
        [int]$MaxRequestHeaderCount , # Default is 100 
        [int]$KeepAliveTimeoutSeconds  , # Default is 130 seconds
        [long]$MaxRequestBufferSize , #default is 1,048,576 bytes (1 MB).
        [int]$MaxRequestHeadersTotalSize , # Default is 32,768 bytes (32 KB)
        [int]$MaxRequestLineSize , # Default is 8,192 bytes (8 KB)
        [long]$MaxResponseBufferSize  , # Default is  65,536 bytes (64 KB).
        [Microsoft.AspNetCore.Server.Kestrel.Core.MinDataRate]$MinRequestBodyDataRate , # Defaults to 240 bytes/second with a 5 second grace period.
        [Microsoft.AspNetCore.Server.Kestrel.Core.MinDataRate]$MinResponseDataRate, # Defaults to 240 bytes/second with a 5 second grace period.
        [int]$RequestHeadersTimeoutSeconds, # Default is 30 seconds.
        [switch]$AllowSynchronousIO, 
        [switch]$DisableResponseHeaderCompression ,
        [switch]$DenyServerHeader,
        [switch]$AllowAlternateSchemes,
        [switch]$AllowHostHeaderOverride,
        [switch]$DisableStringReuse,
        [int]$MaxRunspaces,
        [int]$MinRunspaces = 1
    )
    begin {
        $options = [Kestrun.KestrunOptions]::new()
        if ($MaxRequestBodySize -gt 0) {
            $options.Limits.MaxRequestBodySize = $MaxRequestBodySize
        }
        if ($MaxConcurrentConnections -gt 0) {
            $options.Limits.MaxConcurrentConnections = $MaxConcurrentConnections
        }
        if ($MaxRequestHeaderCount -gt 0) {
            $options.Limits.MaxRequestHeaderCount = $MaxRequestHeaderCount
        }
        if ($KeepAliveTimeoutSeconds -gt 0) {
            $options.Limits.KeepAliveTimeout = [TimeSpan]::FromSeconds($KeepAliveTimeoutSeconds)
        }
        if ($MaxRequestBufferSize -gt 0) {
            $options.Limits.MaxRequestBufferSize = $MaxRequestBufferSize
        }
        if ($MaxRequestHeadersTotalSize -gt 0) {
            $options.Limits.MaxRequestHeadersTotalSize = $MaxRequestHeadersTotalSize
        }
        if ($MaxRequestLineSize -gt 0) {
            $options.Limits.MaxRequestLineSize = $MaxRequestLineSize
        }
        if ($MaxResponseBufferSize -gt 0) {
            $options.Limits.MaxResponseBufferSize = $MaxResponseBufferSize
        }
        if ($null -ne $MinRequestBodyDataRate) {
            $options.Limits.MinRequestBodyDataRate = $MinRequestBodyDataRate
        }
        if ($null -ne $MinResponseDataRate) {
            $options.Limits.MinResponseDataRate = $MinResponseDataRate
        }
        if ($null -ne $RequestHeadersTimeout) {
            $options.Limits.RequestHeadersTimeout = [TimeSpan]::FromSeconds($RequestHeadersTimeoutSeconds)
        }
        if ($AllowSynchronousIO.IsPresent) { 
            $options.AllowSynchronousIO = $AllowSynchronousIO.IsPresent
        }
        if ($DisableResponseHeaderCompression.IsPresent) {
            $options.AllowResponseHeaderCompression = $false
        }
        if ($DenyServerHeader.IsPresent) {
            $options.AddServerHeader = $false
        }
        if ($AllowAlternateSchemes.IsPresent) {
            $options.AllowAlternateSchemes = $true
        }
        if ($AllowHostHeaderOverride.IsPresent) {
            $options.AllowHostHeaderOverride = $true
        }
        if ($DisableStringReuse.IsPresent) {
            $options.DisableStringReuse = $true
        }
        if ($MaxRunspaces -gt 0) {
            $options.MaxRunspaces = $MaxRunspaces
        }
        if ($MinRunspaces -gt 0) {
            $options.MinRunspaces = $MinRunspaces
        }
    }
    process {
        if ($PSCmdlet.ShouldProcess("Kestrun server", "Set server option(s)")) {
            $Server.ConfigureKestrel($options)
        }
    }
}



function Add-KrListener {
    [CmdletBinding(defaultParameterSetName = "NoCert")]
    param(
        [Parameter(Mandatory = $true, ValueFromPipeline = $true)]
        [Kestrun.KestrunHost]$Server,

        [Parameter(Mandatory = $true)]
        [int]$Port,

        [System.Net.IPAddress]$IPAddress = [System.Net.IPAddress]::Any,
        [Parameter(mandatory = $true, ParameterSetName = "CertFile")]
        [string]$CertPath,

        [Parameter(mandatory = $false, ParameterSetName = "CertFile")]
        [SecureString]$CertPassword = $null,

        [Parameter(mandatory = $true, ParameterSetName = "x509Certificate")]
        [System.Security.Cryptography.X509Certificates.X509Certificate2]$X509Certificate = $null,

        [Parameter(ParameterSetName = "x509Certificate")]
        [Parameter(ParameterSetName = "CertFile")]
        [Microsoft.AspNetCore.Server.Kestrel.Core.HttpProtocols]$Protocols,

        [Parameter()]
        [switch]$UseConnectionLogging
    )

    process {
        if ($null -eq $Protocols) {
            if ($PSCmdlet.ParameterSetName -eq "NoCert") {
                $Protocols = [Microsoft.AspNetCore.Server.Kestrel.Core.HttpProtocols]::Http1
            }
            else {
                $Protocols = [Microsoft.AspNetCore.Server.Kestrel.Core.HttpProtocols]::Http1OrHttp2
            }
        }
        if ($PSCmdlet.ParameterSetName -eq "CertFile") {
            if (-not (Test-Path $CertPath)) {
                throw "Certificate file not found: $CertPath"
            }
            $X509Certificate = Import-KestrunCertificate -Path $CertPath -Password $CertPassword
        }


        $Server.ConfigureListener($Port, $IPAddress, $X509Certificate, $Protocols, $UseConnectionLogging.IsPresent)
    }
}


function New-KrServer {
    [CmdletBinding(SupportsShouldProcess = $true)]
    param(
        [Parameter(Mandatory = $true, ValueFromPipeline = $true)]
        [string]$Name
    )
    process {
        $loadedModules = Get-UserImportedModule
        $modulePaths = @($loadedModules | ForEach-Object { $_.Path })
        if ($PSCmdlet.ShouldProcess("Kestrun server '$Name'", "Create new server instance")) {
            $server = [Kestrun.KestrunHost]::new($Name, $script:KestrunRoot, [string[]] $modulePaths)
            return $server
        }
    }
}


function Add-KrRoute {
    [CmdletBinding(defaultParameterSetName = "ScriptBlock")]
    param(
        [Parameter(Mandatory = $true, ValueFromPipeline = $true)]
        [Kestrun.KestrunHost]$Server,
        [Parameter()]
        [Kestrun.HttpVerb[]]$Verbs = @([Kestrun.HttpVerb]::Get),
        [Parameter(Mandatory = $true)]
        [string]$Path,
        [Parameter(Mandatory = $true, ParameterSetName = "ScriptBlock")]
        [ScriptBlock]$ScriptBlock,
        [Parameter(Mandatory = $true, ParameterSetName = "Code")]
        [string]$Code,
        [Parameter(Mandatory = $true, ParameterSetName = "Code")]
        [Kestrun.ScriptLanguage]$Language,
        [Parameter()]
        [string[]]$ExtraImports = $null,
        [Parameter()]
        [System.Reflection.Assembly[]]$ExtraRefs = $null

    )
    begin {
        $Server.ApplyConfiguration()
    }
    process {
        if ($PSCmdlet.ParameterSetName -eq "Code") {
            $Server.AddRoute($Path, $Verbs, $Code, $Language, $ExtraImports, $ExtraRefs)
        }
        else {
            $Server.AddRoute($Path, $Verbs, $ScriptBlock.ToString(), [Kestrun.ScriptLanguage]::PowerShell)
        }
    }
}




function Start-KrServer {
    [CmdletBinding(SupportsShouldProcess = $true)]
    param(
        [Parameter(Mandatory = $true, ValueFromPipeline = $true)]
        [Kestrun.KestrunHost]$Server,
        [Parameter()]
        [switch]$NoWait
    )
    process {
        foreach ($srv in $Server) {
            if ($PSCmdlet.ShouldProcess("Kestrun server", "Start")) {
                # Start the Kestrel server
                Write-Output "Starting Kestrun ..."
                $srv.StartAsync() | Out-Null
                if (-not $NoWait.IsPresent) {
                    # Intercept Ctrl+C and gracefully stop the Kestrun server
                    try {
                        [Console]::TreatControlCAsInput = $true
                        while ($true) {
                            if ([Console]::KeyAvailable) {
                                $key = [Console]::ReadKey($true)
                                if (($key.Modifiers -eq 'Control') -and ($key.Key -eq 'C')) {
                                    Write-Host "Ctrl+C detected. Stopping Kestrun server..."
                                    $srv.StopAsync().Wait()
                                    break
                                }
                            }
                            Start-Sleep -Milliseconds 100
                        }
                    }
                    finally {
                        # Ensure the server is stopped on exit
                        Write-Host "Script exiting. Ensuring server is stopped..."
                        $srv.StopAsync().Wait()
                    }
                }
            }
        }
    }
}
