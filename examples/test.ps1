

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
Set-KrServerOptions -Server $server -MaxRequestBodySize 10485760 -MaxConcurrentConnections 100 -MaxRequestHeaderCount 100 -KeepAliveTimeoutSeconds 120 -AllowSynchronousIO  -DenyServerHeader
 
# Configure the listener (adjust port, cert path, and password)
Add-KrListener -Server $server -Port 5001 -IPAddress ([IPAddress]::Any) -CertPath "cert.pfx" -CertPassword "yourpassword" -Protocols Http1AndHttp2AndHttp3
Add-KrListener -Server $server -Port 5002 -IPAddress ([IPAddress]::Any)  -Protocols Http1

#$server.ApplyConfiguration()
        
 
# Set-KrPythonRuntime

# Add a route with a script block
Add-KrRoute -Server $server -Method "GET" -Path "/json" -ScriptBlock {

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
    Write-KrJsonResponse -inputObject $payload -statusCode 200
}
 

Add-KrRoute -Server $server -Method "GET" -Path "/yaml" -ScriptBlock {

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
    Write-KrYamlResponse -inputObject $payload -statusCode 200
}


Add-KrRoute -Server $server -Method "GET" -Path "/text" -ScriptBlock {

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
        
    } |Format-Table| Out-String
    Write-KrTextResponse -inputObject $payload -statusCode 200
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
 


# ------------------------------------------------------------------
# 1. PowerShell route  ─ /hello-ps
# ------------------------------------------------------------------
Add-KrRoute -Server $server -Path '/hello-ps' -Method GET  -ScriptBlock {
    $Response.ContentType = 'text/plain'
    $Response.Body = "Hello from PowerShell at $(Get-Date -Format o)"
}

# ------------------------------------------------------------------
# 2. C# script route  ─ /hello-cs
#    (wrap the C# source in a here-string *inside* the ScriptBlock)
# ------------------------------------------------------------------
Add-KrRoute -Server $server -Path '/hello-cs' -Language CSharp -Code  @"
using System;
Response.ContentType = "text/plain";
Response.Body        = $"Hello from C# at {DateTime.UtcNow:o}";
"@
 
<#
# ------------------------------------------------------------------
# 3. Python script route  ─ /hello-py
# ------------------------------------------------------------------
Add-KrRoute -Server $server -Path '/hello-py' -Language Python -Code @"
def handle(ctx, res):
    import datetime, platform
    res.ContentType = 'text/plain'
    res.Body        = f'Hello from CPython {platform.python_version()} at {datetime.datetime.utcnow().isoformat()}'
"@ 
 
 
# ------------------------------------------------------------------
# 4. JavaScript (Jint) route  ─ /hello-js
# ------------------------------------------------------------------
Add-KrRoute -Server $server -Path '/hello-js' -Language JavaScript -Code  @"
Response.ContentType = 'text/plain';
Response.Body        = `Hello from JavaScript at ${new Date().toISOString()}`;
"@
 

# ------------------------------------------------------------------
# 5. (optional) F# script route  ─ /hello-fs
#     NB: only if BuildFsDelegate is implemented
# ------------------------------------------------------------------
Add-KrRoute -Server $server -Path '/hello-fs' -Language FSharp -Code  @"
 open System
 Response.ContentType <- "text/plain"
 Response.Body        <- $"Hello from F# at {DateTime.UtcNow:o}"
"@
 
 #>


# Add routes
#$server.AddRoute("/api/echo")
#$server.AddRoute("/api/store")

# Start the server asynchronously
Start-KrServer -Server $server

