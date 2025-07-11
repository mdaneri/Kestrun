
<#
.SYNOPSIS
Configures advanced Kestrun server options for a given KestrunHost instance.

.DESCRIPTION
The Set-KrServerOptions function allows fine-grained configuration of a KestrunHost server instance by setting limits and feature flags. 
It supports tuning performance and security by controlling request body size, concurrent connections, header count, and keep-alive timeouts. 
Feature switches enable or disable synchronous IO, response header compression, and the inclusion of a server header in HTTP responses.

This function is typically used before starting the server to ensure all options are applied. 
It creates a KestrunOptions object, sets properties based on provided parameters, and applies them to the server via ConfigureKestrel.

.PARAMETER Server
The KestrelLib.KestrunHost server instance to configure. This parameter is mandatory and accepts input from the pipeline.

.PARAMETER MaxRequestBodySize
Specifies the maximum allowed size (in bytes) for the request body. Only applied if greater than 0.
Use this to prevent clients from sending excessively large payloads.

.PARAMETER MaxConcurrentConnections
Specifies the maximum number of concurrent connections allowed. Only applied if greater than 0.
Helps control resource usage and prevent overload.

.PARAMETER MaxRequestHeaderCount
Specifies the maximum number of request headers allowed. Only applied if greater than 0.
Can be used to mitigate certain types of HTTP attacks.

.PARAMETER KeepAliveTimeoutSeconds
Specifies the keep-alive timeout in seconds. Only applied if greater than 0.
Controls how long idle connections are kept open.

.PARAMETER AllowSynchronousIO
Enables or disables synchronous IO operations. Set this switch to allow synchronous IO.
Synchronous IO can be useful for legacy code but may reduce scalability.

.PARAMETER AllowResponseHeaderCompression
Enables or disables response header compression. Set this switch to allow response header compression.
Header compression can improve performance but may have security implications.

.PARAMETER AddServerHeader
Enables or disables the addition of the server header in responses. Set this switch to add the server header.
Disabling the server header can help obscure server details for security.

.EXAMPLE
Set-KrServerOptions -Server $server -MaxRequestBodySize 1048576 -MaxConcurrentConnections 100 -AddServerHeader

Configures the server with a 1 MB max request body size, allows up to 100 concurrent connections, and adds the server header to responses.

.EXAMPLE
$server | Set-KrServerOptions -KeepAliveTimeoutSeconds 30 -AllowSynchronousIO

Configures the server to use a 30-second keep-alive timeout and enables synchronous IO.

.NOTES
Requires the KestrelLib.KestrunHost and KestrelLib.KestrunOptions types to be available.
Call this function before starting the server to ensure all options are applied.
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
        [switch]$DisableStringReuse
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