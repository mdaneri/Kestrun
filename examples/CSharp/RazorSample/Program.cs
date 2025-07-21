using Kestrun;
using System.Net;


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
    port: 5000,
    ipAddress: IPAddress.Any,
    protocols: Microsoft.AspNetCore.Server.Kestrel.Core.HttpProtocols.Http1
);
server.ApplyConfiguration();
// 5️⃣ Run!
Console.WriteLine("Open  http://localhost:5000/Hello");

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
