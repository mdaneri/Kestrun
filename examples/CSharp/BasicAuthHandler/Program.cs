using System;
using System.Collections.Generic;
using System.Net;
using Kestrun;
using System.Security;
using System.Security.Cryptography.X509Certificates;
using Org.BouncyCastle.OpenSsl;
using Serilog.Events;
using Serilog;
using Microsoft.AspNetCore.ResponseCompression;
using Org.BouncyCastle.Utilities.Zlib;
using Kestrun.Logging;
using Kestrun.Utilities;   // Only for writing the CSR key

var currentDir = Directory.GetCurrentDirectory();

// 1️⃣  Audit log: only warnings and above, writes JSON files
Log.Logger = new LoggerConfiguration()
        .MinimumLevel.Debug()
        .WriteTo.Console()
        .WriteTo.File("logs/basic_auth_handler.log", rollingInterval: RollingInterval.Day)
        .Register("default", setAsDefault: true);

// 1. Create server
var server = new KestrunHost("MyKestrunServer", currentDir);

// Set Kestrel options
server.Options.ServerOptions.AllowSynchronousIO = false;
server.Options.ServerOptions.AddServerHeader = false; // DenyServerHeader

server.Options.ServerLimits.MaxRequestBodySize = 10485760;
server.Options.ServerLimits.MaxConcurrentConnections = 100;
server.Options.ServerLimits.MaxRequestHeaderCount = 100;
server.Options.ServerLimits.KeepAliveTimeout = TimeSpan.FromSeconds(120);

server.AddResponseCompression(options =>
{
    options.EnableForHttps = true;
    options.MimeTypes = ResponseCompressionDefaults.MimeTypes.Concat(new[]
    {
        "application/json",
        "application/bson",
        "application/cbor",
        "application/yaml",
        "application/xml",
        "text/plain",
        "text/html"
    });
    options.Providers.Add<BrotliCompressionProvider>();
}).AddPowerShellRuntime().AddAuthentication("Basic",  BasicAuthHandler);



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
          DnsNames: ["localhost", "127.0.0.1"],
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
    ipAddress: IPAddress.Loopback,
    x509Certificate: x509Certificate,
    protocols: Microsoft.AspNetCore.Server.Kestrel.Core.HttpProtocols.Http1AndHttp2AndHttp3
);
server.ConfigureListener(
    port: 5000,
    ipAddress: IPAddress.Loopback,
    protocols: Microsoft.AspNetCore.Server.Kestrel.Core.HttpProtocols.Http1
);

server.EnableConfiguration();
/*string pattern,
                                    IEnumerable<HttpMethod> httpMethods,
                                    string scriptBlock,
                                    ScriptLanguage language = ScriptLanguage.PowerShell,
                                    string[]? extraImports = null,
                                    Assembly[]? extraRefs = null)*/
// 4. Add routes
server.AddRoute("/secure/hello", HttpVerb.Get, """
    if (-not $HttpContext.User.Identity.IsAuthenticated) {
        Write-KrErrorResponse -Message "Access denied" -StatusCode 401
        return
    }

    $user = $HttpContext.User.Identity.Name
    $Response.Body = "Welcome, $user! You are authenticated."
    $Response.ContentType = "text/plain"
""", ScriptLanguage.PowerShell);


server.AddRoute("/secure/hello-cs", HttpVerb.Get, """
    if (!Context.User.Identity.IsAuthenticated)
    {
        Response.WriteErrorResponse("Access denied", 401);
        return;
    }

    var user = Context.User.Identity.Name;
    Response.WriteTextResponse($"Welcome, {user}! You are authenticated.", 200);
""", ScriptLanguage.CSharp);

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
