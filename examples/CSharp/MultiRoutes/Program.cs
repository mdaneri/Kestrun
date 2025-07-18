﻿using System;
using System.Collections.Generic;
using System.Net;
using Kestrun;
using System.Security;
using System.Security.Cryptography.X509Certificates; 
using Org.BouncyCastle.OpenSsl;   // Only for writing the CSR key

var currentDir = Directory.GetCurrentDirectory();
var parentDirInfo = Directory.GetParent(currentDir);
if (parentDirInfo == null || parentDirInfo.Parent == null|| parentDirInfo.Parent.Parent == null)
{
    Console.WriteLine("Unable to determine the parent directory for module path.");
    return;
} 
string modulePath = Path.Combine(
    parentDirInfo.Parent.Parent.FullName,
    "src","PowerShell",
    "Kestrun",
    "Kestrun.psm1"
);
Console.WriteLine($"Using Kestrun module from: {modulePath}");
if (!File.Exists(modulePath))
{
    Console.WriteLine($"Kestrun module not found at {modulePath}");
    return;
}

// 1. Create server
var server = new KestrunHost("MyKestrunServer", currentDir, [modulePath]);

// 2. Set server options
var options = new KestrunOptions
{

    AllowSynchronousIO = true,
    AddServerHeader = false // DenyServerHeader
};

options.Limits.MaxRequestBodySize = 10485760;
options.Limits.MaxConcurrentConnections = 100;
options.Limits.MaxRequestHeaderCount = 100;
options.Limits.KeepAliveTimeout = TimeSpan.FromSeconds(120);
server.ConfigureKestrel(options);

X509Certificate2? x509Certificate = null;

if (File.Exists("./devcert.pfx"))
{
    // Import existing certificate
    x509Certificate = CertificateManager.Import(
      "./devcert.pfx",
      "p@ss".AsSpan()
  );
}
else
{
    // Create a new self-signed certificate

    x509Certificate = Kestrun.CertificateManager.NewSelfSigned(
      new Kestrun.CertificateManager.SelfSignedOptions(
          DnsNames: new[] { "localhost", "127.0.0.1" },
          KeyType: Kestrun.CertificateManager.KeyType.Rsa,
          KeyLength: 2048,
          ValidDays: 30,
          Exportable: true
      )
  );
    // Export the certificate to a file
    Kestrun.CertificateManager.Export(
        x509Certificate,
        "./devcert.pfx",
        Kestrun.CertificateManager.ExportFormat.Pfx,
        "p@ss".AsSpan()
    );

}

if (!Kestrun.CertificateManager.Validate(
    x509Certificate,
    checkRevocation: false,
    allowWeakAlgorithms: false,
    denySelfSigned: false,
    strictPurpose: true
))
{
    Console.WriteLine("Certificate validation failed.");
    //Log.Error("Certificate validation failed. Ensure the certificate is valid.");
    Environment.Exit(1);
}



// 3. Add listeners
server.ConfigureListener(
    port: 5001,
    ipAddress: IPAddress.Any,
     x509Certificate: x509Certificate,
    protocols: Microsoft.AspNetCore.Server.Kestrel.Core.HttpProtocols.Http1AndHttp2AndHttp3
);
server.ConfigureListener(
    port: 5002,
    ipAddress: IPAddress.Any,
    protocols: Microsoft.AspNetCore.Server.Kestrel.Core.HttpProtocols.Http1
);

server.ApplyConfiguration();
/*string pattern,
                                    IEnumerable<HttpMethod> httpMethods,
                                    string scriptBlock,
                                    ScriptLanguage language = ScriptLanguage.PowerShell,
                                    string[]? extraImports = null,
                                    Assembly[]? extraRefs = null)*/
// 4. Add routes
server.AddRoute("/ps/json",
            HttpVerb.Get,
            """
            Write-Output "Hello from PowerShell script!" 
            $payload = @{
                Body           = "Hello from PowerShell script! - Json Response(From C#)"
                RequestQuery   = $Request.Query
                RequestHeaders = $Request.Headers
                RequestMethod  = $Request.Method
                RequestPath    = $Request.Path
                # If you want to return the request body, uncomment the next line
                RequestBody    = $Request.Body 
            }
            Write-KrJsonResponse -InputObject $payload -StatusCode 200
            """,
            ScriptLanguage.PowerShell);

server.AddRoute("/ps/yaml", HttpVerb.Get, """
            Write-Output "Hello from PowerShell script! - Yaml Response(From C#)" 
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
        """, Kestrun.ScriptLanguage.PowerShell);

server.AddRoute("/ps/xml", HttpVerb.Get, """
            Write-Output "Hello from PowerShell script! - Xml Response(From C#)" 
            $payload = @{
                Body           = "Hello from PowerShell script!"
                RequestQuery   = $Request.Query
                RequestHeaders = $Request.Headers
                RequestMethod  = $Request.Method
                RequestPath    = $Request.Path
                # If you want to return the request body, uncomment the next line
                RequestBody    = $Request.Body 
                
            } 
            Write-KrXmlResponse -inputObject $payload -statusCode 200
        """, Kestrun.ScriptLanguage.PowerShell);

server.AddRoute("/ps/text", HttpVerb.Get, """        
            Write-Output "Hello from PowerShell script! - Text Response(From C#)" 
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
        """, Kestrun.ScriptLanguage.PowerShell);

server.AddRoute("/ps/stream/binary", HttpVerb.Get, """        
                Write-Output 'Hello from PowerShell script! - stream Binary file Response'
                Write-KrFileResponse -FilePath '../Files/LargeFiles/2GB.bin' -statusCode 200      
            """, Kestrun.ScriptLanguage.PowerShell);


server.AddRoute("/ps/stream/text", HttpVerb.Get, """        
                Write-Output 'Hello from PowerShell script! - stream Text file Response'
                Write-KrFileResponse -FilePath '../Files/LargeFiles/2GB.txt' -statusCode 200      
            """, Kestrun.ScriptLanguage.PowerShell);

server.AddRoute("/hello-ps", HttpVerb.Get, """
            $Response.ContentType = 'text/plain'
            $Response.Body = "Hello from PowerShell at $(Get-Date -Format o)"
        """, Kestrun.ScriptLanguage.PowerShell);

server.AddRoute("/hello-cs", HttpVerb.Get, """
            Response.ContentType = "text/plain";
            Response.Body = $"Hello from C# at {DateTime.Now:O}";
        """, Kestrun.ScriptLanguage.CSharp);


server.AddRoute("/cs/json", HttpVerb.Get, """
            Console.WriteLine("Hello from C# script! - Json Response(From C#)");
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

        """, Kestrun.ScriptLanguage.CSharp);

server.AddRoute("/cs/yaml", HttpVerb.Get, """
            Console.WriteLine("Hello from C# script! - Yaml Response(From C#)");
            var payload = new
            {
                Body = "Hello from C# script! - Yaml Response",
                RequestQuery = Request.Query,
                RequestHeaders = Request.Headers,
                RequestMethod = Request.Method,
                RequestPath = Request.Path,
                RequestBody = Request.Body
            };

            Response.WriteYamlResponse( payload,  200,
                contentType: "text/yaml" );
            Response.ContentDisposition.Type = ContentDispositionType.Inline;
            Response.ContentDisposition.FileName = "response.yaml";
        """, Kestrun.ScriptLanguage.CSharp);

server.AddRoute("/cs/xml", HttpVerb.Get, """
            Console.WriteLine("Hello from C# script! - Xml Response(From C#)");
            var payload = new
            {
                Body = "Hello from C# script! - Yaml Response",
                RequestQuery = Request.Query,
                RequestHeaders = Request.Headers,
                RequestMethod = Request.Method,
                RequestPath = Request.Path,
                RequestBody = Request.Body
            };
            Response.WriteXmlResponse( payload,  200);
        """, Kestrun.ScriptLanguage.CSharp);

server.AddRoute("/cs/text", HttpVerb.Get, """
            Console.WriteLine("Hello from C# script! - Text Response(From C#)");
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

        """, Kestrun.ScriptLanguage.CSharp);


server.AddRoute("/cs/stream/binary", HttpVerb.Get, """        
                Console.WriteLine("Hello from C# script! - stream Binaryfile Response(From C#)");
               Response.WriteFileResponse(filePath: "../Files/LargeFiles/2GB.bin", contentType: "application/octet-stream", statusCode: 200);
            """, Kestrun.ScriptLanguage.CSharp);

server.AddRoute("/cs/stream/text", HttpVerb.Get, """        
                Console.WriteLine("Hello from C# script! - stream Text file Response(From C#)");
               Response.WriteFileResponse(filePath: "../Files/LargeFiles/2GB.txt", contentType: "text/plain", statusCode: 200);
            """, Kestrun.ScriptLanguage.CSharp);

server.AddNativeRoute("/compiled", HttpVerb.Get, async (req, res) =>
{
    res.WriteJsonResponse(new { ok = true, message = "Native C# works!" });
    await Task.Yield();
});

server.AddNativeRoute("/compiled/stream", HttpVerb.Get, async (req, res) =>
{
    res.WriteFileResponse(filePath: "../Files/LargeFiles/2GB.bin", contentType: "application/octet-stream", statusCode: 200);
    await Task.Yield();
});


// 5. Start the server
server.StartAsync().Wait();

Console.WriteLine("Kestrun server started. Press Ctrl+C to stop.");
// drive our “keep alive” loop
var keepRunning = true;
Console.CancelKeyPress += (s, e) =>
{
    // tell Console not to kill process immediately
    e.Cancel = true;
    Console.WriteLine("Stopping Kestrun server…");
    server.StopAsync().Wait();
    keepRunning = false; // set flag to exit loop 
};

// loop until keepRunning is cleared
while (keepRunning)
{
    Thread.Sleep(1000);
}

Console.WriteLine("Server has shut down.");
server.Dispose();
Environment.Exit(0); // Force process exit if needed
