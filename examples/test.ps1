

try {
    # Determine the script path and Kestrun module path
    $ScriptPath = (Split-Path -Parent -Path $MyInvocation.MyCommand.Path)
    $kestrunPath = Split-Path -Parent -Path $ScriptPath

    # Import the Kestrun module from the source path if it exists, otherwise from installed modules
    if (Test-Path -Path "$($kestrunPath)/src/Kestrun.psm1" -PathType Leaf) {
        Import-Module "$($kestrunPath)/src/Kestrun.psm1" -Force -ErrorAction Stop
    }
    else {
        Import-Module -Name 'Kestrun' -MaximumVersion 2.99 -ErrorAction Stop
    }
}
catch { throw }

 

$server = New-KrServer -Name "MyKestrunServer" 
# Example usage:
Set-KrServerOptions -Server $server -MaxRequestBodySize 10485760 -MaxConcurrentConnections 100 -MaxRequestHeaderCount 100 -KeepAliveTimeoutSeconds 120 -AllowSynchronousIO  -AllowResponseHeaderCompression  -AddServerHeader
 
# Configure the listener (adjust port, cert path, and password)
 Add-KrListener -Server $server -Port 5001 -IPAddress ([IPAddress]::Any) -CertPath "cert.pfx" -CertPassword "yourpassword" -Protocols Http1AndHttp2AndHttp3
Add-KrListener -Server $server -Port 5002 -IPAddress ([IPAddress]::Any)  -Protocols Http1

#$server.ApplyConfiguration()
        
 

# Add a route with a script block
Add-KrRoute -Server $server -Method "GET" -Path "/echo" -ScriptBlock {

    Write-Output "Hello from PowerShell script!"
    $RequestJson = $Request | ConvertTo-Json
    Write-Output "Request JSON: $RequestJson"
    $payload = @{
        Body           = "Hello from PowerShell script!"
        RequestQuery   = $Request.Query
        RequestHeaders = $Request.Headers
        RequestMethod  = $Request.Method
        RequestPath    = $Request.Path
        # If you want to return the request body, uncomment the next line
        RequestBody    = $Request.Body 
        
    }
    Write-KrJsonResponse -inputObject $payload -statusCode 201
}
 
Add-KrRoute -Server $server -Method "GET" -Path "/messagestream" -ScriptBlock {
    $DebugPreference = 'Continue' 
    $VerbosePreference = 'Continue'

    Write-Output "Hello from PowerShell script!"
    Write-Warning "This is a warning message."  
    Write-Verbose "This is a verbose message."
    Write-Error "This is an error message." 
    Write-Host "This is a host message."
    Write-Information "This is an information message."  
    Write-Debug "This is a debug message."  
}
 
# Add routes
#$server.AddRoute("/api/echo")
#$server.AddRoute("/api/store")

# Start the server asynchronously
Start-KrServer -Server $server

