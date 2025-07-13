using System;
using System.Collections.Generic;
using System.Net;
using KestrumLib;

class Program
{
    static void Main(string[] args)
    {
        var currentDir = Directory.GetCurrentDirectory();
        var parentDirInfo = Directory.GetParent(currentDir);
        if (parentDirInfo == null || parentDirInfo.Parent == null)
        {
            Console.WriteLine("Unable to determine the parent directory for module path.");
            return;
        }
        string modulePath = Path.Combine(
            parentDirInfo.Parent.FullName,
            "src",
            "Kestrun.psm1"
        );
        Console.WriteLine($"Using Kestrun module from: {modulePath}");
        if (!File.Exists(modulePath))
        {
            Console.WriteLine($"Kestrun module not found at {modulePath}");
            return;
        }

        // 1. Create server
        var server = new KestrunHost("MyKestrunServer", new[]
        {
            modulePath
        });

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

        // 3. Add listeners
        server.ConfigureListener(
            port: 5001,
            ipAddress: IPAddress.Any,
            certPath: "cert.pfx",
            certPassword: "yourpassword",
            protocols: Microsoft.AspNetCore.Server.Kestrel.Core.HttpProtocols.Http1AndHttp2AndHttp3
        );
        server.ConfigureListener(
            port: 5002,
            ipAddress: IPAddress.Any,
            certPath: null,
            certPassword: null,
            protocols: Microsoft.AspNetCore.Server.Kestrel.Core.HttpProtocols.Http1
        );

        server.ApplyConfiguration();

        // 4. Add routes
        server.AddRoute("/ps/json", """
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
            Write-KrJsonResponse -inputObject $payload -statusCode 200
        """, KestrumLib.ScriptLanguage.PowerShell, "GET");

        server.AddRoute("/ps/yaml", """
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
        """, KestrumLib.ScriptLanguage.PowerShell, "GET");

        server.AddRoute("/ps/text", """        
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
        """, KestrumLib.ScriptLanguage.PowerShell, "GET");


        server.AddRoute("/hello-ps", """
            $Response.ContentType = 'text/plain'
            $Response.Body = "Hello from PowerShell at $(Get-Date -Format o)"
        """, KestrumLib.ScriptLanguage.PowerShell, "GET");

        server.AddRoute("/hello-cs", """
            Response.ContentType = "text/plain";
            Response.Body = $"Hello from C# at {DateTime.Now:O}";
        """, KestrumLib.ScriptLanguage.CSharp, "GET");


        server.AddRoute("/cs/json", """
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

        """, KestrumLib.ScriptLanguage.CSharp, "GET");

        server.AddRoute("/cs/yaml", """
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

            Response.WriteYamlResponse( payload,  200);

        """, KestrumLib.ScriptLanguage.CSharp, "GET");


          server.AddRoute("/cs/text", """
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

        """, KestrumLib.ScriptLanguage.CSharp, "GET");

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
    }
}