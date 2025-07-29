using System;
using System.Collections.Generic;
using System.Collections;
using System.Net;
using Kestrun;
using System.Security;
using System.Security.Cryptography.X509Certificates;
using Org.BouncyCastle.OpenSsl;
using System.Collections.Concurrent;
using Serilog;
using Kestrun.Logging;
using Microsoft.Extensions.Logging;
using Kestrun.Utilities;
using Kestrun.SharedState;
using System.Text;   // Only for writing the CSR key


var currentDir = Directory.GetCurrentDirectory();
new LoggerConfiguration()
      .MinimumLevel.Debug()
      .WriteTo.File("logs/sharedState.log", rollingInterval: RollingInterval.Day)
      .Register("Audit", setAsDefault: true);

// 1. Create server 

var server = new KestrunHost("Kestrun SharedState", currentDir);
// Set Kestrel options
server.Options.ServerOptions.AllowSynchronousIO = false;
server.Options.ServerOptions.AddServerHeader = false; // DenyServerHeader

server.Options.ServerLimits.MaxRequestBodySize = 10485760;
server.Options.ServerLimits.MaxConcurrentConnections = 100;
server.Options.ServerLimits.MaxRequestHeaderCount = 100;
server.Options.ServerLimits.KeepAliveTimeout = TimeSpan.FromSeconds(120);


// 3. Configure listeners
server.ConfigureListener(
    port: 5000,
    ipAddress: IPAddress.Any,
    protocols: Microsoft.AspNetCore.Server.Kestrel.Core.HttpProtocols.Http1
);

server.AddPowerShellRuntime();

var sharedVisits = new Hashtable();
sharedVisits["Count"] = 0;
// 3.1 Inject global variable
if (!SharedStateStore.Set("Visits", sharedVisits))
{
    Console.WriteLine("Failed to define global variable 'Visits'.");
    Environment.Exit(1);
}
server.EnableConfiguration();
// 4. Add routes
server.AddMapRoute("/ps/show", HttpVerb.Get,
"""
    # $Visits is available      

    Write-KrTextResponse -inputObject "Runspace: $([runspace]::DefaultRunspace.Name) - Visits(type:$($Visits.GetType().Name)) so far: $($Visits["Count"])" -statusCode 200
""",
            ScriptLanguage.PowerShell);

server.AddMapRoute("/ps/visit", HttpVerb.Get,
"""
    # increment the injected variable
    $Visits["Count"]++
    Write-KrTextResponse -inputObject "Runspace: $(([runspace]::DefaultRunspace).Name) - Incremented Visits(type:$($Visits.GetType().Name)) to $($Visits["Count"])" -statusCode 200
""", Kestrun.ScriptLanguage.PowerShell);


server.AddMapRoute("/cs/show", HttpVerb.Get,
"""
    // $Visits is available
    Context.Response.WriteTextResponse($"Visits so far: {Visits["Count"]}", 200);
""",
ScriptLanguage.CSharp);

server.AddMapRoute("/cs/visit", HttpVerb.Get, """
    // increment the injected variable
    Visits["Count"] = ((int)Visits["Count"]) + 1;

     Context.Response.WriteTextResponse($"Incremented to {Visits["Count"]}", 200);
""", Kestrun.ScriptLanguage.CSharp);

server.AddNativeRoute("/raw", HttpVerb.Get, async (ctx) =>
{
    Console.WriteLine("Native C# route hit!");

    SharedStateStore.TryGet("Visits", out Hashtable? visits);

    int visitCount = visits != null && visits["Count"] != null ? (visits["Count"] as int? ?? 0) : 0;

    if (visits != null && visits["Count"] != null)
    {
        await ctx.Response.WriteTextResponseAsync($"Visits so far: {visitCount}", 200);
    }
    else
    {
        await ctx.Response.WriteErrorResponseAsync("Visits variable not found or invalid.", 500);
    }
});

await server.RunUntilShutdownAsync(
    consoleEncoding: Encoding.UTF8,
    onStarted: () => Console.WriteLine("Server ready 🟢"),
    onShutdownError: ex => Console.WriteLine($"Shutdown error: {ex.Message}"

    )
);