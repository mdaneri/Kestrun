
[Diagnostics.CodeAnalysis.SuppressMessageAttribute('PSAvoidUsingConvertToSecureStringWithPlainText', '')]
param()
<#
.SYNOPSIS
    Kestrun PowerShell Example: Multi Routes
.DESCRIPTION
    This script demonstrates how to define multiple routes in Kestrun, a PowerShell web server framework.
#>

try {
    $ScriptPath = (Split-Path -Parent -Path $MyInvocation.MyCommand.Path)
    # Determine the script path and Kestrun module path
    $examplesPath = (Split-Path -Parent ($ScriptPath))
    $kestrunPath = Split-Path -Parent -Path $examplesPath
    $kestrunModulePath = "$kestrunPath/src/PowerShell/Kestrun/Kestrun.psm1"
    # Import the Kestrun module from the source path if it exists, otherwise from installed modules
    if (Test-Path -Path $kestrunModulePath -PathType Leaf) {
        Import-Module $kestrunModulePath -Force -ErrorAction Stop
    }
    else {
        Import-Module -Name 'Kestrun' -MaximumVersion 2.99 -ErrorAction Stop
    }
}
catch {
    Write-Error "Failed to import Kestrun module: $_"
    Write-Error "Ensure the Kestrun module is installed or the path is correct."
    exit 1
}


$server = New-KrServer -Name "MyKestrunServer"

if (Test-Path "$ScriptPath\devcert.pfx" ) {
    $cert = Import-KsCertificate -FilePath ".\devcert.pfx" -Password (convertTo-SecureString -String 'p@ss' -AsPlainText -Force)
}
else {
    $cert = New-KsSelfSignedCertificate -DnsName 'localhost' -Exportable
    Export-KsCertificate -Certificate $cert `
        -FilePath "$ScriptPath\devcert" -Format pfx -IncludePrivateKey -Password (convertTo-SecureString -String 'p@ss' -AsPlainText -Force)
}

if (-not (Test-KsCertificate -Certificate $cert )) {
    Write-Error "Certificate validation failed. Ensure the certificate is valid and not self-signed."
    exit 1
}

# Example usage:
Set-KrServerOption -Server $server  -AllowSynchronousIO  -DenyServerHeader
 
Set-KrServerLimit -Server $server  -MaxRequestBodySize 10485760 -MaxConcurrentConnections 100 -MaxRequestHeaderCount 100 -KeepAliveTimeoutSeconds 120
# Configure the listener (adjust port, cert path, and password)
Add-KrListener -Server $server -Port 5001 -IPAddress ([IPAddress]::Any) -X509Certificate $cert -Protocols Http1AndHttp2AndHttp3
Add-KrListener -Server $server -Port 5000 -IPAddress ([IPAddress]::Any)

Add-KrResponseCompression -Server $server -EnableForHttps -MimeTypes @("text/plain", "text/html", "application/json", "application/xml", "application/x-www-form-urlencoded")
Add-KrPowerShellRuntime -Server $server

# Enable configuration
Enable-KrConfiguration -Server $server
 #$server.ApplyConfiguration()

# Set-KrPythonRuntime

# Add a route with a script block
Add-KrMapRoute -Server $server -Verbs Get -Path "/ps/json" -ScriptBlock {

    Write-Output "Hello from PowerShell script! - Json Response"
    # Payload
    $payload = @{
        Body           = "Hello from PowerShell script! - Json Response"
        RequestQuery   = $Request.Query
        RequestHeaders = $Request.Headers
        RequestMethod  = $Request.Method
        RequestPath    = $Request.Path
        # If you want to return the request body, uncomment the next line
        RequestBody    = $Request.Body
    }
    Write-KrJsonResponse -inputObject $payload -statusCode 200
}


Add-KrMapRoute -Server $server -Verbs Get -Path "/ps/bson" -ScriptBlock {

    Write-Output "Hello from PowerShell script! - Bson Response"
    # Payload
    $payload = @{
        Body           = "Hello from PowerShell script! - Bson Response"
        RequestQuery   = $Request.Query
        RequestHeaders = $Request.Headers
        RequestMethod  = $Request.Method
        RequestPath    = $Request.Path
        # If you want to return the request body, uncomment the next line
        RequestBody    = $Request.Body
    }
    Write-KrBsonResponse -inputObject $payload -statusCode 200
}

Add-KrMapRoute -Server $server -Verbs Get -Path "/ps/cbor" -ScriptBlock {

    Write-Output "Hello from PowerShell script! - Cbor Response"
    # Payload
    $payload = @{
        Body           = "Hello from PowerShell script! - Cbor Response"
        RequestQuery   = $Request.Query
        RequestHeaders = $Request.Headers
        RequestMethod  = $Request.Method
        RequestPath    = $Request.Path
        # If you want to return the request body, uncomment the next line
        RequestBody    = $Request.Body
    }
    Write-KrCborResponse -inputObject $payload -statusCode 200
}
 
 

Add-KrMapRoute -Server $server -Verbs Get -Path "/ps/yaml" -ScriptBlock {

    Write-Output "Hello from PowerShell script! - Yaml Response" 
    # Payload
    $payload = @{
        Body           = "Hello from PowerShell script! - Yaml Response"
        RequestQuery   = $Request.Query
        RequestHeaders = $Request.Headers
        RequestMethod  = $Request.Method
        RequestPath    = $Request.Path
        # If you want to return the request body, uncomment the next line
        RequestBody    = $Request.Body
    } 
    Write-KrYamlResponse -inputObject $payload -statusCode 200
}


Add-KrMapRoute -Server $server -Verbs Get -Path "/ps/xml" -ScriptBlock {

    Write-Output "Hello from PowerShell script! - Xml Response"
    # Payload
    $payload = @{
        Body           = "Hello from PowerShell script! - Xml Response"
        RequestQuery   = $Request.Query
        RequestHeaders = $Request.Headers
        RequestMethod  = $Request.Method
        RequestPath    = $Request.Path
        # If you want to return the request body, uncomment the next line
        RequestBody    = $Request.Body

    }
    Write-KrXmlResponse -inputObject $payload -statusCode 200
}


Add-KrMapRoute -Server $server -Verbs Get -Path "/ps/text" -ScriptBlock {

    Write-Output "Hello from PowerShell script! - Text Response"
    # Payload
    $payload = @{
        Body           = "Hello from PowerShell script! - Text Response"
        RequestQuery   = $Request.RequestQuery
        RequestHeaders = $Request.Headers
        RequestMethod  = $Request.Method
        RequestPath    = $Request.Path
        # If you want to return the request body, uncomment the next line
        RequestBody    = $Request.Body
    } | Format-Table | Out-String
    Write-KrTextResponse -inputObject $payload -statusCode 200
}


Add-KrMapRoute -Server $server -Verbs Get -Path "/ps/file" -ScriptBlock {

    Write-Output "Hello from PowerShell script! - file Response"
    Write-KrFileResponse -FilePath "..\..\README.md" -FileDownloadName "README.md" -ContentDisposition Inline -statusCode 200 -ContentType "text/markdown"
}

Add-KrMapRoute -Server $server -Verbs Get -Path "/cs/xml" -Language CSharp -Code @"

            Console.WriteLine("Hello from C# script! - Xml Response(From PowerShell)");
            var payload = new
            {
                Body = "Hello from C# script! - Xml Response",
                RequestQuery = Request.Query,
                RequestHeaders = Request.Headers,
                RequestMethod = Request.Method,
                RequestPath = Request.Path,
                RequestBody = Request.Body
            };
            Response.WriteXmlResponse( payload,  200);
"@

Add-KrMapRoute -Server $server -Verbs Get -Path "/cs/json" -Language CSharp -Code @"

            Console.WriteLine("Hello from C# script! - Json Response(From PowerShell)");
            var payload = new
            {
                Body = "Hello from C# script! - Json Response",
                RequestQuery = Request.Query,
                RequestHeaders = Request.Headers,
                RequestMethod = Request.Method,
                RequestPath = Request.Path,
                RequestBody = Request.Body
            };
            Response.WriteJsonResponse( payload,  200);
"@

Add-KrMapRoute -Server $server -Verbs Get -Path "/cs/bson" -Language CSharp -Code @"

            Console.WriteLine("Hello from C# script! - Bson Response(From PowerShell)");
            var payload = new
            {
                Body = "Hello from C# script! - Bson Response",
                RequestQuery = Request.Query,
                RequestHeaders = Request.Headers,
                RequestMethod = Request.Method,
                RequestPath = Request.Path,
                RequestBody = Request.Body
            };
            Response.WriteBsonResponse( payload,  200);
"@

Add-KrMapRoute -Server $server -Verbs Get -Path "/cs/cbor" -Language CSharp -Code @"

            Console.WriteLine("Hello from C# script! - Cbor Response(From PowerShell)");
            var payload = new
            {
                Body = "Hello from C# script! - Cbor Response",
                RequestQuery = Request.Query,
                RequestHeaders = Request.Headers,
                RequestMethod = Request.Method,
                RequestPath = Request.Path,
                RequestBody = Request.Body
            };
            Response.WriteCborResponse( payload,  200);
"@

Add-KrMapRoute -Server $server -Verbs Get -Path "/cs/yaml" -Language CSharp -Code @"

            Console.WriteLine("Hello from C# script! - Yaml Response(From PowerShell)");
            var payload = new
            {
                Body = "Hello from C# script! - Yaml Response",
                RequestQuery = Request.Query,
                RequestHeaders = Request.Headers,
                RequestMethod = Request.Method,
                RequestPath = Request.Path,
                RequestBody = Request.Body
            };
            Response.WriteYamlResponse( payload,  200);
"@

Add-KrMapRoute -Server $server -Verbs Get -Path "/cs/text" -Language CSharp -Code @"

            Console.WriteLine("Hello from C# script! - Text Response(From PowerShell)");
            var payload = new
            {
                Body = "Hello from C# script! - Text Response",
                RequestQuery = Request.Query,
                RequestHeaders = Request.Headers,
                RequestMethod = Request.Method,
                RequestPath = Request.Path,
                RequestBody = Request.Body
            };
            Response.WriteTextResponse( payload,  200);
"@


Add-KrMapRoute -Server $server -Verbs Get -Path "/cs/file" -Language CSharp -Code @"

    Console.WriteLine("Hello from C# script! - file Response(From C#)");
    Response.WriteFileResponse("..\\..\\README.md", null, 200);
"@

Add-KrMapRoute -Server $server -Verbs Get -Path "/messagestream" -ScriptBlock {
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
Add-KrMapRoute -Server $server -Verbs Get -Path '/hello-ps' -ScriptBlock {
    $Response.ContentType = 'text/plain'
    $Response.Body = "Hello from PowerShell at $(Get-Date -Format o)"
}

# ------------------------------------------------------------------
# 2. C# script route  ─ /hello-cs
#    (wrap the C# source in a here-string *inside* the ScriptBlock)
# ------------------------------------------------------------------
Add-KrMapRoute -Server $server -Verbs Get -Path '/hello-cs' -Language CSharp -Code  @"
using System;
Response.ContentType = "text/plain";
Response.Body        = $"Hello from C# at {DateTime.UtcNow:o}";
"@
 
<#
# ------------------------------------------------------------------
# 3. Python script route  ─ /hello-py
# ------------------------------------------------------------------
Add-KrMapRoute -Server $server -Path '/hello-py' -Language Python -Code @"
def handle(ctx, res):
    import datetime, platform
    res.ContentType = 'text/plain'
    res.Body        = f'Hello from CPython {platform.python_version()} at {datetime.datetime.utcnow().isoformat()}'
"@ 
 
 
# ------------------------------------------------------------------
# 4. JavaScript (Jint) route  ─ /hello-js
# ------------------------------------------------------------------
Add-KrMapRoute -Server $server -Path '/hello-js' -Language JavaScript -Code  @"
Response.ContentType = 'text/plain';
Response.Body        = `Hello from JavaScript at ${new Date().toISOString()}`;
"@
 

# ------------------------------------------------------------------
# 5. (optional) F# script route  ─ /hello-fs
#     NB: only if BuildFsDelegate is implemented
# ------------------------------------------------------------------
Add-KrMapRoute -Server $server -Path '/hello-fs' -Language FSharp -Code  @"
 open System
 Response.ContentType <- "text/plain"
 Response.Body        <- $"Hello from F# at {DateTime.UtcNow:o}"
"@
 
 #>


# Add routes
#$server.AddMapRoute("/api/echo")
#$server.AddMapRoute("/api/store")

# Start the server asynchronously
Start-KrServer -Server $server

