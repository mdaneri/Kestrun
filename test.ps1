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

function Split-PodeBuildPwshPath {
    # Check if OS is Windows, then split PSModulePath by ';', otherwise use ':'
    if ($IsWindows) {
        return $env:PSModulePath -split ';'
    }
    else {
        return $env:PSModulePath -split ':'
    }
}

function Add-AspNetCoreType {
    param (
        [Parameter()]
        [ValidateSet("net8", "net9", "net10")]
        [string]$Version = "net8"
    )
    $versionNumber=$Version.Substring(3)
    $path = Split-Path -Path (Get-Command -Name "dotnet.exe").Source -Parent
    if (-not $path) {
        throw "Could not determine the path to the dotnet executable."
    }
    $baseDir = Join-Path -Path $path -ChildPath "shared" -AdditionalChildPath "Microsoft.AspNetCore.App"
    if (Test-Path -Path $baseDir -PathType Container) {
        $versionDirs = Get-ChildItem -Path $baseDir -Directory | Where-Object { $_.Name -like "$($versionNumber).*" } | Sort-Object Name -Descending
        foreach ($verDir in $versionDirs) {
            $assemblyPath = Join-Path -Path $verDir.FullName -ChildPath "Microsoft.AspNetCore.dll"
            if (Test-Path -Path $assemblyPath) {
                Assert-AssemblyLoaded -AssemblyPath $assemblyPath
                return
            }
        }
       
    }
    throw "Microsoft.AspNetCore.App version $Version.* not found in PSModulePath."
}

# root path
$root = Split-Path -Parent -Path $MyInvocation.MyCommand.Path 
# Usage
Add-AspNetCoreType -Version "net8"
# Add-AspNetCoreType -Version "net8.0.*"

# root path
$root = Split-Path -Parent -Path $MyInvocation.MyCommand.Path 
# Assert that the assembly is loaded
Assert-AssemblyLoaded "$root\Kestrel\bin\Debug\net8.0\Kestrel.dll"
 
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

#Add-KRListener -Server $server -Port 5001 -IPAddress ([IPAddress]::Any) -CertPath "cert.pfx" -CertPassword "yourpassword" -Protocols Http1AndHttp2AndHttp3
Add-KRListener -Server $server -Port 5002 -IPAddress ([IPAddress]::Any)  -Protocols Http1
# Configure the listener (adjust port, cert path, and password)
#$server.ConfigureListener(  5001,[IPAddress]::Any, "cert.pfx", "yourpassword", [Microsoft.AspNetCore.Server.Kestrel.Core.HttpProtocols]::Http1AndHttp2AndHttp3, $false)
#$server.ConfigureListener(  5002,[IPAddress]::Any, "cert.pfx", "yourpassword", [Microsoft.AspNetCore.Server.Kestrel.Core.HttpProtocols]::Http1AndHttp2AndHttp3, $false)

$server.ApplyConfiguration()
        
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

# Start the server asynchronously
$server.StartAsync() | Out-Null

# Intercept Ctrl+C and gracefully stop the Kestrel server
try {
    [Console]::TreatControlCAsInput = $true
    while ($true) {
        if ([Console]::KeyAvailable) {
            $key = [Console]::ReadKey($true)
            if (($key.Modifiers -eq 'Control') -and ($key.Key -eq 'C')) {
                Write-Host "Ctrl+C detected. Stopping Kestrel server..."
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

