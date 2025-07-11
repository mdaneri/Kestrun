
function Set-KRServerOptions {
    param(
        [Parameter(Mandatory = $true, ValueFromPipeline = $true)]
        [KestrelLib.KestrelServer]$Server,
        [int]$MaxRequestBodySize = 10485760,
        [int]$MaxConcurrentConnections = 100,
        [int]$MaxRequestHeaderCount = 100,
        [int]$KeepAliveTimeoutSeconds = 120,
        [switch]$AllowSynchronousIO,
        [switch]$AllowResponseHeaderCompression,
        [switch]$AddServerHeader
    )
    $options = @{
        Limits                           = @{
            "MaxRequestBodySize"       = $MaxRequestBodySize
            "MaxConcurrentConnections" = $MaxConcurrentConnections
            "MaxRequestHeaderCount"    = $MaxRequestHeaderCount
            "KeepAliveTimeout"         = [TimeSpan]::FromSeconds($KeepAliveTimeoutSeconds)
        } 
        "AllowSynchronousIO"             = $AllowSynchronousIO.IsPresent
        "AllowResponseHeaderCompression" = $AllowResponseHeaderCompression.IsPresent
        "AddServerHeader"                = $AddServerHeader.IsPresent
    } 
    $Server.ConfigureKestrel($options)
}



function Add-KRListener {
    param(
        [Parameter(Mandatory = $true, ValueFromPipeline = $true)]
        [KestrelLib.KestrelServer]$Server,
        [Parameter(Mandatory = $true)]
        [int]$Port,
        [System.Net.IPAddress]$IPAddress = [System.Net.IPAddress]::Any,
        [string]$CertPath = $null,
        [string]$CertPassword = $null,
        [Microsoft.AspNetCore.Server.Kestrel.Core.HttpProtocols]$Protocols = [Microsoft.AspNetCore.Server.Kestrel.Core.HttpProtocols]::Http1AndHttp2, 
        [switch]$UseConnectionLogging 
    )
    $Server.ConfigureListener($Port, $IPAddress, $CertPath, $CertPassword, $Protocols, $UseConnectionLogging.IsPresent)
}


function New-KRServer {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true)]
        [string]$Name
    )
    $server = [KestrelLib.KestrelServer]::new($Name)
    return $server
}


function Add-KRRoute {
    param(
        [Parameter(Mandatory = $true, ValueFromPipeline = $true)]
        [KestrelLib.KestrelServer]$Server,
        [Parameter()]
        [string]$Method = "GET",
        [Parameter(Mandatory = $true)]
        [string]$Path,
        [Parameter(Mandatory = $true)]
        [ScriptBlock]$ScriptBlock
    )
    $Server.AddRoute($Path, $ScriptBlock.ToString(), $Method)
}