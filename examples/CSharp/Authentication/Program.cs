using System;
using System.Collections.Generic;
using System.Net;
using Kestrun;
using System.Security;
using System.Security.Cryptography.X509Certificates;
using Org.BouncyCastle.OpenSsl;
using Serilog.Events;
using Serilog;
using System.Text;
using System.Security.Claims;
using System.Management.Automation;
using Kestrun.Certificates;
using Kestrun.Logging;
using Kestrun.Utilities;
using Kestrun.Scripting;
using Kestrun.Hosting;
using Kestrun.Authentication;
using Microsoft.IdentityModel.Tokens;
using Microsoft.AspNetCore.ResponseCompression;
using Microsoft.IdentityModel.JsonWebTokens;   // JsonWebTokenHandler 
using Kestrun.Security;          // ISecurityTokenValidator


/*
$creds   = "admin:password"
$basic   = "Basic " + [Convert]::ToBase64String(
                       [Text.Encoding]::ASCII.GetBytes($creds))
$token   = (Invoke-RestMethod https://localhost:5001/token/new -SkipCertificateCheck -Headers @{ Authorization = $basic }).access_token
Invoke-RestMethod https://localhost:5001/secure/ps/hello -SkipCertificateCheck -Headers @{Authorization=$basic}
Invoke-RestMethod https://localhost:5001/secure/cs/hello -SkipCertificateCheck -Headers @{Authorization=$basic}
Invoke-RestMethod https://localhost:5001/secure/native/hello -SkipCertificateCheck -Headers @{Authorization=$basic}
 
Invoke-RestMethod https://localhost:5001/secure/key/simple/hello -SkipCertificateCheck -Headers @{ "X-Api-Key" = "my-secret-api-key" }
Invoke-RestMethod https://localhost:5001/secure/key/ps/hello -SkipCertificateCheck -Headers @{ "X-Api-Key" = "my-secret-api-key" }
Invoke-RestMethod https://localhost:5001/secure/key/cs/hello -SkipCertificateCheck -Headers @{ "X-Api-Key" = "my-secret-api-key" }

Invoke-RestMethod https://localhost:5001/secure/jwt/hello -SkipCertificateCheck -Headers @{ Authorization = "Bearer $token" }

Invoke-RestMethod https://localhost:5001/token/renew -SkipCertificateCheck -Headers @{ Authorization = "Bearer $token" }
*/
var currentDir = Directory.GetCurrentDirectory();

// 1️⃣  Audit log: only warnings and above, writes JSON files
Log.Logger = new LoggerConfiguration()
        .MinimumLevel.Debug()
        .WriteTo.Console()
        .WriteTo.File("logs/basic_auth_handler.log", rollingInterval: RollingInterval.Day)
        .Register("default", setAsDefault: true);

// 1. Create server
var server = new KestrunHost("Kestrun Authentication", currentDir);
// Set Kestrel options
server.Options.ServerOptions.AllowSynchronousIO = false;
server.Options.ServerOptions.AddServerHeader = false; // DenyServerHeader

server.Options.ServerLimits.MaxRequestBodySize = 10485760;
server.Options.ServerLimits.MaxConcurrentConnections = 100;
server.Options.ServerLimits.MaxRequestHeaderCount = 100;
server.Options.ServerLimits.KeepAliveTimeout = TimeSpan.FromSeconds(120);

const string BasicPowershellScheme = "PowershellBasic";
const string BasicNativeScheme = "NativeBasic";
const string BasicCSharpScheme = "CSharpBasic";
const string JwtScheme = "Bearer";
const string ApiKeySimple = "ApiKeySimple";
const string ApiKeyPowerShell = "ApiKeyPowerShell";
const string ApiKeyCSharp = "ApiKeyCSharp";
string issuer = "KestrunApi";
string audience = "KestrunClients";
// 1) 32-byte hex or ascii secret  (use a vault / env var in production)
// shared secret = 32-byte array

const string JwtKeyHex =
    "6f1a1ce2e8cc4a5685ad0e1d1f0b8c092b6dce4f7a08b1c2d3e4f5a6b7c8d9e0"; // 32-byte hex string
byte[] keyBytes = Convert.FromHexString(JwtKeyHex);
string keyB64u = Base64UrlEncoder.Encode(keyBytes);   // <-- supply this
string textKey = System.Text.Encoding.UTF8.GetString(keyBytes);
 var tokenBuilder = JwtTokenBuilder.New()
               //          .WithSubject("admin")
               .WithIssuer(issuer)
               .WithAudience(audience)
               .SignWithSecret(keyB64u, JwtAlgorithm.HS256)   // GCM enc

               .ValidFor(TimeSpan.FromHours(1));


var builderResult = tokenBuilder.Build();
 

// for the token 

//var symKey = new SymmetricSecurityKey(keyBytes);


// 2. Add services
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

.AddBasicAuthentication(BasicPowershellScheme, opts =>
{
    opts.Realm = "Power-Kestrun";

    opts.CodeSettings = new AuthenticationCodeSettings
    {
        Language = ScriptLanguage.PowerShell,
        Code = """
            param(
                [string]$Username,
                [string]$Password
            )
            if ($Username -eq 'admin' -and $Password -eq 'password') {
                return $true
            } else {
                return $false
            }
        """
    };
    opts.Base64Encoded = true;            // default anyway
    opts.RequireHttps = false;           // example

})
.AddBasicAuthentication(BasicNativeScheme, opts =>
{
    opts.Realm = "Native-Kestrun";
    opts.ValidateCredentials = async (context, username, password) =>
    {
        Log.Information("Validating credentials for {Username}", username);
        // pretend we did some async work:
        await Task.Yield();
        // Replace with your real credential validation logic
        return username == "admin" && password == "password";
    };
}).AddBasicAuthentication(BasicCSharpScheme, opts =>
{
    opts.Realm = "CSharp-Kestrun";
    opts.CodeSettings = new AuthenticationCodeSettings
    {
        Language = ScriptLanguage.CSharp,

        Code = """      
        return username == "admin" && password == "password";
    """
    };
}).AddApiKeyAuthentication(ApiKeySimple, opts =>
{
    opts.HeaderName = "X-Api-Key";
    opts.ExpectedKey = "my-secret-api-key";
}).AddApiKeyAuthentication(ApiKeyPowerShell, opts =>
{
    opts.HeaderName = "X-Api-Key";
    opts.CodeSettings = new AuthenticationCodeSettings
    {
        Language = ScriptLanguage.PowerShell,
        Code = """
    param(
        [string]$ProvidedKey
    )
    if ($ProvidedKey -eq 'my-secret-api-key') {
        return $true
    } else {
        return $false
    }
    """
    };
}).AddApiKeyAuthentication(ApiKeyCSharp, opts =>
{
    opts.HeaderName = "X-Api-Key";
    opts.CodeSettings = new AuthenticationCodeSettings
    {
        Language = ScriptLanguage.CSharp,
        Code = """
    return FixedTimeEquals.Test(providedKeyBytes, "my-secret-api-key");
    // or use a simple string comparison: // or use a simple string comparison:  
    // return providedKey == "my-secret-api-key";
    """,
        ExtraImports = ["System.Text"]
    };
})

// ── JWT – HS256 or HS512, RS256, etc. ─────────────────────────────────
.AddJwtBearerAuthentication(
    scheme: JwtScheme,
     validationParameters: builderResult.ValidationParameters());

/*new TokenValidationParameters
{
    ValidateIssuer = true,
    ValidIssuer = issuer,
    ValidateAudience = true,
    ValidAudience = audience,
    ValidateLifetime = true,
    ClockSkew = TimeSpan.FromMinutes(1),

    RequireSignedTokens = true,
    ValidateIssuerSigningKey = true,
    IssuerSigningKey = symKey,   // ← only this one key neede
    ValidAlgorithms = [SecurityAlgorithms.HmacSha256]

});*/

// issuer: issuer,
//audience: audience,
//validationKey: jwtKey,
//validAlgorithms: [SecurityAlgorithms.HmacSha256]);


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
    x509Certificate = CertificateManager.NewSelfSigned(
      new CertificateManager.SelfSignedOptions(
          DnsNames: ["localhost", "127.0.0.1"],
          KeyType: CertificateManager.KeyType.Rsa,
          KeyLength: 2048,
          ValidDays: 30,
          Exportable: true
      )
  );
    // Export the certificate to a file
    CertificateManager.Export(
        x509Certificate,
        "./devcert.pfx",
        CertificateManager.ExportFormat.Pfx,
        "p@ss".AsSpan()
    );

}

if (!CertificateManager.Validate(
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
server.AddMapRoute("/secure/ps/hello", HttpVerb.Get, """
    if (-not $Context.HttpContext.User.Identity.IsAuthenticated) {
        Write-KrErrorResponse -Message "Access denied" -StatusCode 401
        return
    }

    $user = $Context.HttpContext.User.Identity.Name
    Write-KrTextResponse -InputObject "Welcome, $user! You are authenticated by PowerShell Code." -ContentType "text/plain"
""", ScriptLanguage.PowerShell, [BasicPowershellScheme]);

server.AddMapRoute("/secure/cs/hello", HttpVerb.Get, """
    if (-not $Context.HttpContext.User.Identity.IsAuthenticated) {
        Write-KrErrorResponse -Message "Access denied" -StatusCode 401
        return
    }

    $user = $Context.HttpContext.User.Identity.Name
    Write-KrTextResponse -InputObject "Welcome, $user! You are authenticated by C# Code." -ContentType "text/plain"
""", ScriptLanguage.PowerShell, [BasicCSharpScheme]);


server.AddMapRoute("/secure/native/hello", HttpVerb.Get, """
    if (-not $Context.HttpContext.User.Identity.IsAuthenticated) {
        Write-KrErrorResponse -Message "Access denied" -StatusCode 401
        return
    }

    $user = $Context.HttpContext.User.Identity.Name
    Write-KrTextResponse -InputObject "Welcome, $user! You are authenticated by Native C# code." -ContentType "text/plain"
""", ScriptLanguage.PowerShell, [BasicNativeScheme]);


server.AddNativeRoute("/secure/key/simple/hello", HttpVerb.Get, async (ctx) =>
{
    var user = ctx.HttpContext.User?.Identity?.Name;
    await ctx.Response.WriteTextResponseAsync($"Welcome, {user}! You are authenticated using simple key matching.", 200);

}, [ApiKeySimple]);


server.AddMapRoute("/secure/key/ps/hello", HttpVerb.Get, """
    if (-not $Context.HttpContext.User.Identity.IsAuthenticated) {
        Write-KrErrorResponse -Message "Access denied" -StatusCode 401
        return
    }

    $user = $Context.HttpContext.User.Identity.Name
    Write-KrTextResponse -InputObject "Welcome, $user! You are authenticated by PowerShell Code."
    
""", ScriptLanguage.PowerShell, [ApiKeyPowerShell]);


server.AddMapRoute("/secure/key/cs/hello", HttpVerb.Get, """
    if (!Context.HttpContext.User.Identity.IsAuthenticated)
    {
        Context.Response.WriteErrorResponse("Access denied", 401);
        return;
    }

    var user = Context.HttpContext.User.Identity.Name;
    Context.Response.WriteTextResponse($"Welcome, {user}! You are authenticated by C# code.", 200);
""", ScriptLanguage.CSharp, [ApiKeyCSharp]);


server.AddMapRoute("/secure/jwt/hello", HttpVerb.Get, """
    if (!Context.HttpContext.User.Identity.IsAuthenticated)
    {
        Context.Response.WriteErrorResponse("Access denied", 401);
        return;
    }

    var user = Context.HttpContext.User.Identity.Name;
    Context.Response.WriteTextResponse($"Welcome, {user}! You are authenticated.", 200);
""", ScriptLanguage.CSharp, [JwtScheme]);


server.AddNativeRoute("/token/renew", HttpVerb.Get, async (ctx) =>
{
    var token = await builderResult.RenewAsync(TimeSpan.FromHours(1));
    await ctx.Response.WriteJsonResponseAsync(new { access_token = token });

}, [JwtScheme]);


server.AddNativeRoute("/token/new", HttpVerb.Get, async (ctx) =>
{
// tiny demo – replace with your real credential check
/*    var auth = ctx.Request.Authorization;
    if (auth != "Basic YWRtaW46czNjcjN0")
    {   // “admin:s3cr3t” base64
        ctx.Response.WriteErrorResponse("Access denied", 401);
        return;
    }*/

// var creds = new SigningCredentials(jwtKey, SecurityAlgorithms.HmacSha256);
try
{
    var build = tokenBuilder.WithSubject("admin").Build();
    var token = build.Token();

    await ctx.Response.WriteJsonResponseAsync(new { access_token = token , ExpiresIn = 3600}); // 1 hour TTL
    Log.Information("Generated new JWT token: {Token}", token);
    }
    catch (Exception ex)
    {
        Log.Error(ex, "Failed to generate JWT token");
        await ctx.Response.WriteErrorResponseAsync("Internal Server Error", 500);
    }
}, [BasicNativeScheme]);

await server.RunUntilShutdownAsync(
    consoleEncoding: Encoding.UTF8,
    onStarted: () => Console.WriteLine("Server ready 🟢"),
    onShutdownError: ex => Console.WriteLine($"Shutdown error: {ex.Message}"

    )
);

