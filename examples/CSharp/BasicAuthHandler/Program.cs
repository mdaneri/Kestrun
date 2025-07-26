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



/*
$creds   = "admin:s3cr3t"
$basic   = "Basic " + [Convert]::ToBase64String(
                       [Text.Encoding]::ASCII.GetBytes($creds))
$token   = (Invoke-RestMethod https://localhost:5001/token -SkipCertificateCheck -Headers @{ Authorization = $basic }).access_token
Invoke-RestMethod https://localhost:5001/secure/hello -SkipCertificateCheck -Headers @{Authorization=$basic}
Invoke-RestMethod https://localhost:5001//secure/hello-cs -SkipCertificateCheck -Headers @{ Authorization = "Bearer $token" }

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
}).AddAuthentication(
    //  defaultScheme: BasicScheme,          // only matters if you don’t call RequireAuthorization("…")
    buildSchemes: ab =>
    {
        // ←── BASIC ──
        ab.AddScheme<AuthenticationSchemeOptions, BasicAuthHandler>(BasicScheme, null);

        // ←── JWT ──
        ab.AddJwtBearer(
            JwtScheme,
            configureOptions: opts =>
            {
                opts.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidIssuer = issuer,
                    ValidateAudience = true,
                    ValidAudience = audience,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = jwtKey,
                    ClockSkew = TimeSpan.FromMinutes(1)
                };
            }
        );
    }
).AddPowerShellRuntime();//.AddAuthorization();


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
    if (-not $Context.User.Identity.IsAuthenticated) {
        Write-KrErrorResponse -Message "Access denied" -StatusCode 401
        return
    }

    $user = $Context.User.Identity.Name
    $Response.Body = "Welcome, $user! You are authenticated."
    $Response.ContentType = "text/plain"
""", ScriptLanguage.PowerShell, [BasicScheme]);


server.AddRoute("/secure/hello-cs", HttpVerb.Get, """
    if (!Context.User.Identity.IsAuthenticated)
    {
        Response.WriteErrorResponse("Access denied", 401);
        return;
    }

    var user = Context.User.Identity.Name;
    Response.WriteTextResponse($"Welcome, {user}! You are authenticated.", 200);
""", ScriptLanguage.CSharp, [JwtScheme]);




server.AddNativeRoute("/token", HttpVerb.Get, async (req, res) =>
{
    // tiny demo – replace with your real credential check
    var auth = req.Authorization;
    if (auth != "Basic YWRtaW46czNjcjN0")
    {   // “admin:s3cr3t” base64
        res.WriteErrorResponse("Access denied", 401);
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
        res.WriteJsonResponse(new { access_token = token });
    }
    catch (Exception ex)
    {
        Log.Error(ex, "Failed to generate JWT token");
        res.WriteErrorResponse("Internal Server Error", 500);
    }
    await Task.Yield();
});

await server.RunUntilShutdownAsync(
    consoleEncoding: Encoding.UTF8,
    onStarted: () => Console.WriteLine("Server ready 🟢"),
    onShutdownError: ex => Console.WriteLine($"Shutdown error: {ex.Message}"

    )
);

