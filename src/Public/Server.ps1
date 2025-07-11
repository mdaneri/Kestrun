
function Set-KrServerOptions {
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



function Add-KrListener {
    param(
        [Parameter(Mandatory = $true, ValueFromPipeline = $true)]
        [KestrelLib.KestrelServer]$Server,
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
    $server = [KestrelLib.KestrelServer]::new($Name, $modulePaths)
    return $server
}


function Add-KrRoute {
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
    $server.ApplyConfiguration()
    $Server.AddRoute($Path, $ScriptBlock.ToString(), $Method)
}




function Start-KrServer {
    param(
        [Parameter(Mandatory = $true, ValueFromPipeline = $true)]
        [KestrelLib.KestrelServer]$Server,
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