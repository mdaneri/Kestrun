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
using Kestrun.Utilities;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using System.Security.Claims;
using System.IdentityModel.Tokens.Jwt;   // Only for writing the CSR key
using Kestrun.Hosting;
using System.Management.Automation;


/*
$creds   = "admin:s3cr3t"
$basic   = "Basic " + [Convert]::ToBase64String(
                       [Text.Encoding]::ASCII.GetBytes($creds))
$token   = (Invoke-RestMethod https://localhost:5001/token -SkipCertificateCheck -Headers @{ Authorization = $basic }).access_token
Invoke-RestMethod https://localhost:5001/secure/hello -SkipCertificateCheck -Headers @{Authorization=$basic}
Invoke-RestMethod https://localhost:5001/secure/hello-cs -SkipCertificateCheck -Headers @{ Authorization = "Bearer $token" }

*/
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

const string BasicScheme = "Basic";
const string JwtScheme = "Bearer";    // or "MyJwt", or whatever label you prefer

string issuer = "KestrunApi";
string audience = "KestrunClients";
// 1) 32-byte hex or ascii secret  (use a vault / env var in production)
const string JwtKeyHex = "6f1a1ce2e8cc4a5685ad0e1d1f0b8c092b6dce4f7a08b1c2d3e4f5a6b7c8d9e0";
byte[] jwtKeyBytes = Convert.FromHexString(JwtKeyHex);
var jwtKey = new SymmetricSecurityKey(jwtKeyBytes);

var code = """
              param(
                    [string]$Username,
                    [string]$Password
                )
              if ($Username -eq 'admin' -and $Password -eq 's3cr3t') {
                  return $true
              } else {
                  return $false
              }
""";
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
}).AddPowerShellRuntime()
// ── BASIC (generic helper) ──────────────────────────────────────────── 

.AddBasicAuthentication(BasicScheme, options =>
{
    options.Realm = "My Kestrun Server";
    options.ValidateCredentials = async (context, username, password) =>
    {
        try
        {
            if (!context.Items.ContainsKey("PS_INSTANCE"))
            {
                throw new InvalidOperationException("PowerShell runspace not found in context items. Ensure PowerShellRunspaceMiddleware is registered.");
            }
            PowerShell ps = context.Items["PS_INSTANCE"] as PowerShell
                  ?? throw new InvalidOperationException("PowerShell instance not found in context items.");
            if (ps.Runspace == null)
            {
                throw new InvalidOperationException("PowerShell runspace is not set. Ensure PowerShellRunspaceMiddleware is registered.");
            }


            ps.AddScript(code, useLocalScope: true)
            .AddParameter("username", username)
            .AddParameter("password", password);
            var psResults = await ps.InvokeAsync().ConfigureAwait(false);

            if (psResults.Count == 0 || psResults[0] == null || psResults[0].BaseObject is not bool isValid)
            {
                Log.Error("PowerShell script did not return a valid boolean result.");
                return false;
            }
            Log.Information("Basic authentication result for {Username}: {IsValid}", username, isValid);
            return isValid;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error during Basic authentication for {Username}", username);
            return false;
        }
    };
})

/*
.AddBasicAuthentication(BasicScheme, options =>
{
    options.Realm = "My Kestrun Server";
    options.ValidateCredentials = async (context, username, password) =>
    {
        // pretend we did some async work:
        await Task.Yield();
        // Replace with your real credential validation logic
        return username == "admin" && password == "s3cr3t";
    };
})
*/

// ── JWT – HS256 or HS512, RS256, etc. ─────────────────────────────────
.AddJwtBearerAuthentication(
    scheme: JwtScheme,
    issuer: issuer,
    audience: audience,
    validationKey: jwtKey,
    validAlgorithms: [SecurityAlgorithms.HmacSha256]);


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
server.AddMapRoute("/secure/hello", HttpVerb.Get, """
    if (-not $Context.HttpContext.User.Identity.IsAuthenticated) {
        Write-KrErrorResponse -Message "Access denied" -StatusCode 401
        return
    }

    $user = $Context.HttpContext.User.Identity.Name
    $Context.Response.Body = "Welcome, $user! You are authenticated."
    $Context.Response.ContentType = "text/plain"
""", ScriptLanguage.PowerShell, [BasicScheme]);


server.AddMapRoute("/secure/hello-cs", HttpVerb.Get, """
    if (!Context.HttpContext.User.Identity.IsAuthenticated)
    {
        Context.Response.WriteErrorResponse("Access denied", 401);
        return;
    }

    var user = Context.HttpContext.User.Identity.Name;
    Context.Response.WriteTextResponse($"Welcome, {user}! You are authenticated.", 200);
""", ScriptLanguage.CSharp, [JwtScheme]);




server.AddNativeRoute("/token", HttpVerb.Get, async (ctx) =>
{
    // tiny demo – replace with your real credential check
    var auth = ctx.Request.Authorization;
    if (auth != "Basic YWRtaW46czNjcjN0")
    {   // “admin:s3cr3t” base64
        ctx.Response.WriteErrorResponse("Access denied", 401);
        return;
    }

    var claims = new[]
    {
        new Claim(JwtRegisteredClaimNames.Sub, "admin"),
        new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString("N"))
    };

    var creds = new SigningCredentials(jwtKey, SecurityAlgorithms.HmacSha256);
    var jwt = new JwtSecurityToken(
                   issuer: issuer,
                   audience: audience,
                   claims: claims,
                   expires: DateTime.UtcNow.AddHours(1),
                   signingCredentials: creds);
    try
    {
        var token = new JwtSecurityTokenHandler().WriteToken(jwt);
        ctx.Response.WriteJsonResponse(new { access_token = token });
    }
    catch (Exception ex)
    {
        Log.Error(ex, "Failed to generate JWT token");
        ctx.Response.WriteErrorResponse("Internal Server Error", 500);
    }
    await Task.Yield();
});

await server.RunUntilShutdownAsync(
    consoleEncoding: Encoding.UTF8,
    onStarted: () => Console.WriteLine("Server ready 🟢"),
    onShutdownError: ex => Console.WriteLine($"Shutdown error: {ex.Message}"

    )
);

