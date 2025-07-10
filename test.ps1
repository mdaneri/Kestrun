function Assert-AssemblyLoaded {
    param (
        [string]$AssemblyPath
    )
    $assemblyName = [System.Reflection.AssemblyName]::GetAssemblyName($AssemblyPath).Name
    $loaded = [AppDomain]::CurrentDomain.GetAssemblies() | Where-Object { $_.GetName().Name -eq $assemblyName }
    if (-not $loaded) {
        Add-Type -Path $AssemblyPath
    }
}

# Usage
Assert-AssemblyLoaded "C:\Program Files\dotnet\shared\Microsoft.AspNetCore.App\8.0.18\Microsoft.AspNetCore.dll"
Assert-AssemblyLoaded "C:\Users\mdaneri\Documents\GitHub\Kestrun\Kestrel\bin\Debug\net8.0\Kestrel.dll"
 
# Create an instance of the KestrelServer class
$server = [KestrelLib.KestrelServer]::new()

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

# Example usage:
Set-KRServerOptions -Server $server -MaxRequestBodySize 10485760 -MaxConcurrentConnections 100 -MaxRequestHeaderCount 100 -KeepAliveTimeoutSeconds 120 -AllowSynchronousIO  -AllowResponseHeaderCompression  -AddServerHeader

# Configure the listener (adjust port, cert path, and password)
$server.ConfigureListener(5001, "cert.pfx", "yourpassword")
# Add a GET route using MapGet
$script = @'
Write-Host "Hello from PowerShell script!"
$RequestJson = $Request | ConvertTo-Json
Write-Host "Request JSON: $RequestJson" 
$Response = @{
    StatusCode = 200
    Body = "Hello from PowerShell script!"
    RequestQuery = $Request.Query
    RequestHeaders = $Request.Headers
    RequestMethod = $Request.Method
    RequestPath = $Request.Path
    # If you want to return the request body, uncomment the next line
    RequestBody = $Request.Body 
    Headers = @{
        "Content-Type" = "text/plain"
    }
}
$Response | ConvertTo-Json  

'@
$server.AddRoute("/echo", $script, "GET")

# Add routes
#$server.AddRoute("/api/echo")
#$server.AddRoute("/api/store")

# Start the server
$server.Run()

 