 
<#
.SYNOPSIS
    Configures advanced options and operational limits for a Kestrun server instance.

.DESCRIPTION
    The Set-KrServerOptions function allows fine-grained configuration of a Kestrun server instance. 
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

.EXAMPLE
    Set-KrServerOptions -Server $srv -MaxRequestBodySize 1000000
    Configures the server instance $srv to limit request body size to 1,000,000 bytes.

.NOTES
    All parameters are optional except for -Server. 
    Defaults are based on typical Kestrun server settings as of the latest release.
#>
function Set-KrServerOptions {
    param(
        [Parameter(Mandatory = $true, ValueFromPipeline = $true)]
        [KestrelLib.KestrunHost]$Server,
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
        [int]$MaxRunspaces
    )
    $options = [KestrelLib.KestrunOptions]::new() 
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
    #RequestHeaderEncodingSelector 
    #ResponseHeaderEncodingSelector

    #limits.http2
    #limits.http3
    $Server.ConfigureKestrel($options)
}



function Add-KrListener {
    param(
        [Parameter(Mandatory = $true, ValueFromPipeline = $true)]
        [KestrelLib.KestrunHost]$Server,
        [Parameter(Mandatory = $true)]
        [int]$Port,
        [System.Net.IPAddress]$IPAddress = [System.Net.IPAddress]::Any,
        [string]$CertPath = $null,
        [string]$CertPassword = $null,
        [Microsoft.AspNetCore.Server.Kestrel.Core.HttpProtocols]$Protocols = [Microsoft.AspNetCore.Server.Kestrel.Core.HttpProtocols]::Http1, 
        [switch]$UseConnectionLogging 
    )
    if ($null -eq $CertPath -and $Protocols -ne [Microsoft.AspNetCore.Server.Kestrel.Core.HttpProtocols]::Http1) {
        throw "CertPath must be provided when using protocols other than Http1."
    }
    $Server.ConfigureListener($Port, $IPAddress, $CertPath, $CertPassword, $Protocols, $UseConnectionLogging.IsPresent)
}


function New-KrServer {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true)]
        [string]$Name
    )
    $loadedModules = Get-UserImportedModule
    $modulePaths = @($loadedModules | ForEach-Object { $_.Path })
    $server = [KestrelLib.KestrunHost]::new($Name, $modulePaths)
    return $server
}


function Add-KrRoute {
    param(
        [Parameter(Mandatory = $true, ValueFromPipeline = $true)]
        [KestrelLib.KestrunHost]$Server,
        [Parameter()]
        [string]$Method = "GET",
        [Parameter(Mandatory = $true)]
        [string]$Path,
        [Parameter(Mandatory = $true)]
        [ScriptBlock]$ScriptBlock
    )
    $server.ApplyConfiguration()
     # $Server.AddRoute($Path, $ScriptBlock.ToString(), [KestrelLib.ScriptLanguage]::PowerShell , $Method)
   $Server.AddRoute($Path, $ScriptBlock.ToString(), $Method)
}




function Start-KrServer {
    param(
        [Parameter(Mandatory = $true, ValueFromPipeline = $true)]
        [KestrelLib.KestrunHost]$Server,
        [Parameter()]
        [switch]$NoWait
    ) 
    # Start the Kestrel server
    Write-Output "Starting Kestrun ..."
    $Server.StartAsync() | Out-Null
    if (-not $NoWait.IsPresent) {
        # Intercept Ctrl+C and gracefully stop the Kestrun server
        try {
            [Console]::TreatControlCAsInput = $true
            while ($true) {
                if ([Console]::KeyAvailable) {
                    $key = [Console]::ReadKey($true)
                    if (($key.Modifiers -eq 'Control') -and ($key.Key -eq 'C')) {
                        Write-Host "Ctrl+C detected. Stopping Kestrun server..."
                        $server.StopAsync().Wait()
                        break
                    }
                }
                Start-Sleep -Milliseconds 100
            }
        }
        finally {
            # Ensure the server is stopped on exit
            Write-Host "Script exiting. Ensuring server is stopped..."
            $server.StopAsync().Wait()
        }
    }
}



function Write-KrJsonResponse {
    param(
        [Parameter(Mandatory = $true)]
        [object]$inputObject,
        [Parameter()]
        [int]$StatusCode = 200,
        [Parameter()]
        [ValidateRange(0, 100)]
        [int]$Depth = 10
    )
    $Response.Body = $inputObject | ConvertTo-Json -Depth $Depth
    $Response.ContentType = "application/json"
    $Response.StatusCode = $StatusCode
}   