using System;
using System.Collections.Generic;
using System.Collections;
using System.Net;
using Kestrun;
using System.Security;
using System.Security.Cryptography.X509Certificates;
using Org.BouncyCastle.OpenSsl;   // Only for writing the CSR key


var currentDir = Directory.GetCurrentDirectory();
var parentDirInfo = Directory.GetParent(currentDir);
if (parentDirInfo == null || parentDirInfo.Parent == null || parentDirInfo.Parent.Parent == null)
{
    Console.WriteLine("Unable to determine the parent directory for module path.");
    return;
}
string modulePath = Path.Combine(
    parentDirInfo.Parent.Parent.FullName,
    "src", "PowerShell",
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

    AllowSynchronousIO = false, // Disable synchronous IO
    AddServerHeader = false // DenyServerHeader
};

options.Limits.MaxRequestBodySize = 10485760;
options.Limits.MaxConcurrentConnections = 100;
options.Limits.MaxRequestHeaderCount = 100;
options.Limits.KeepAliveTimeout = TimeSpan.FromSeconds(120);
server.ConfigureKestrel(options);

// 3. Configure listeners
server.ConfigureListener(
    port: 5002,
    ipAddress: IPAddress.Any,
    protocols: Microsoft.AspNetCore.Server.Kestrel.Core.HttpProtocols.Http1
);

var sharedVisits = new Hashtable();
sharedVisits["Count"] = 0;
// 3.1 Inject global variable
if (!server.SharedState.Set("Visits", sharedVisits))
{
    Console.WriteLine("Failed to define global variable 'Visits'.");
    Environment.Exit(1);
}
server.ApplyConfiguration();
// 4. Add routes
server.AddRoute("/ps/show", HttpVerb.Get,
"""
    # $Visits is available      

    Write-KrTextResponse -inputObject "Runspace: $([runspace]::DefaultRunspace.Name) - Visits(type:$($Visits.GetType().Name)) so far: $($Visits["Count"])" -statusCode 200
""",
            ScriptLanguage.PowerShell);

server.AddRoute("/ps/visit", HttpVerb.Get,
"""
    # increment the injected variable
    $Visits["Count"]++
    Write-KrTextResponse -inputObject "Runspace: $(([runspace]::DefaultRunspace).Name) - Incremented Visits(type:$($Visits.GetType().Name)) to $($Visits["Count"])" -statusCode 200
""", Kestrun.ScriptLanguage.PowerShell);


server.AddRoute("/cs/show", HttpVerb.Get,
"""
    // $Visits is available
    Response.WriteTextResponse($"Visits so far: {Visits["Count"]}", 200);
""",
ScriptLanguage.CSharp);

server.AddRoute("/cs/visit", HttpVerb.Get, """
    // increment the injected variable
    Visits["Count"] = ((int)Visits["Count"]) + 1;

    Response.WriteTextResponse($"Incremented to {Visits["Count"]}", 200);
""", Kestrun.ScriptLanguage.CSharp);

server.AddNativeRoute("/raw", HttpVerb.Get, async (req, res) =>
{
    Console.WriteLine("Native C# route hit!");

    server.SharedState.TryGet("Visits", out Hashtable? visits);

    int visitCount = visits != null && visits["Count"] is int v ? v : 0;

    if (visits != null && visits["Count"] is int)
    {
        res.WriteTextResponse($"Visits so far: {visitCount}", 200);
    }
    else
    {
        res.WriteErrorResponse("Visits variable not found or invalid.", 500);
    }
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
