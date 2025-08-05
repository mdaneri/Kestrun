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
using Kestrun.Security;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Negotiate;          // ISecurityTokenValidator


/*
$creds   = "admin:password"
$basic   = "Basic " + [Convert]::ToBase64String(
                       [Text.Encoding]::ASCII.GetBytes($creds))
$token   = (Invoke-RestMethod https://localhost:5001/token/new -SkipCertificateCheck -Headers @{ Authorization = $basic }).access_token
Invoke-RestMethod https://localhost:5001/secure/ps/hello -SkipCertificateCheck -Headers @{Authorization=$basic}
Invoke-RestMethod https://localhost:5001/secure/cs/hello -SkipCertificateCheck -Headers @{Authorization=$basic}
Invoke-RestMethod https://localhost:5001/secure/vb/hello -SkipCertificateCheck -Headers @{Authorization=$basic}
Invoke-RestMethod https://localhost:5001/secure/native/hello -SkipCertificateCheck -Headers @{Authorization=$basic}
 
Invoke-RestMethod https://localhost:5001/secure/key/simple/hello -SkipCertificateCheck -Headers @{ "X-Api-Key" = "my-secret-api-key" }
Invoke-RestMethod https://localhost:5001/secure/key/ps/hello -SkipCertificateCheck -Headers @{ "X-Api-Key" = "my-secret-api-key" }
Invoke-RestMethod https://localhost:5001/secure/key/cs/hello -SkipCertificateCheck -Headers @{ "X-Api-Key" = "my-secret-api-key" }
Invoke-RestMethod https://localhost:5001/secure/key/vb/hello -SkipCertificateCheck -Headers @{ "X-Api-Key" = "my-secret-api-key" }

Invoke-RestMethod https://localhost:5001/secure/jwt/hello -SkipCertificateCheck -Headers @{ Authorization = "Bearer $token" }

$token2 = (Invoke-RestMethod https://localhost:5001/token/renew -SkipCertificateCheck -Headers @{ Authorization = "Bearer $token" }).access_token
Invoke-RestMethod https://localhost:5001/secure/jwt/hello -SkipCertificateCheck -Headers @{ Authorization = "Bearer $token2" }

#Form
Invoke-WebRequest -Uri https://localhost:5001/cookies/login -SkipCertificateCheck -Method Post -Body @{ username = 'admin'; password = 'secret' } -SessionVariable authSession
Invoke-WebRequest -Uri https://localhost:5001/cookies/secure -SkipCertificateCheck -WebSession $authSession 
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
const string BasicVBNetScheme = "VBNetBasic";
const string JwtScheme = "Bearer";
const string ApiKeySimple = "ApiKeySimple";
const string ApiKeyPowerShell = "ApiKeyPowerShell";
const string ApiKeyCSharp = "ApiKeyCSharp";
const string ApiKeyVBNet = "ApiKeyVBNet";
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

/// Add compression
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
})

/// Enable PowerShell runtime
.AddPowerShellRuntime()

/// ── BASIC AUTHENTICATION – POWERSHELL CODE ─────────────────────────────
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

/// ── BASIC AUTHENTICATION – NATIVE C# CODE ──────────────────────────────
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
})

/// ── BASIC AUTHENTICATION – C# CODE ────────────────────────────────────
.AddBasicAuthentication(BasicCSharpScheme, opts =>
{
    opts.Realm = "CSharp-Kestrun";
    opts.CodeSettings = new AuthenticationCodeSettings
    {
        Language = ScriptLanguage.CSharp,

        Code = """      
        return username == "admin" && password == "password";
    """
    };
})
/// ── BASIC AUTHENTICATION – C# CODE ────────────────────────────────────
.AddBasicAuthentication(BasicVBNetScheme, opts =>
{
    opts.Realm = "VBNet-Kestrun";
    opts.CodeSettings = new AuthenticationCodeSettings
    {
        Language = ScriptLanguage.VBNet,

        Code = """      
        Return username = "admin" AndAlso password = "password"
    """
    };
})

/// ── WINDOWS AUTHENTICATION ────────────────────────────────────────────
.AddWindowsAuthentication()

/// ── API KEY AUTHENTICATION – SIMPLE STRING MATCHING ────────────────────
.AddApiKeyAuthentication(ApiKeySimple, opts =>
{
    opts.HeaderName = "X-Api-Key";
    opts.ExpectedKey = "my-secret-api-key";
})

/// ── API KEY AUTHENTICATION – POWERSHELL CODE ───────────────────────────
.AddApiKeyAuthentication(ApiKeyPowerShell, opts =>
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
})

/// ── API KEY AUTHENTICATION – C# CODE ───────────────────────────────────
.AddApiKeyAuthentication(ApiKeyCSharp, opts =>
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
.AddApiKeyAuthentication(ApiKeyVBNet, opts =>
{
    opts.HeaderName = "X-Api-Key";
    opts.CodeSettings = new AuthenticationCodeSettings
    {
        Language = ScriptLanguage.VBNet,
        Code = """
    Return FixedTimeEquals.Test(providedKeyBytes, "my-secret-api-key")
    ' or use a simple string comparison:
    ' Return providedKey = "my-secret-api-key"
    """,
        ExtraImports = ["System.Text"]
    };
}) 

/// ── JWT AUTHENTICATION – C# CODE ───────────────────────────────────────
.AddJwtBearerAuthentication(
    scheme: JwtScheme,
     validationParameters: builderResult.GetValidationParameters())

/// ── COOKIE AUTHENTICATION – C# CODE ────────────────────────────────────
.AddCookieAuthentication(
    scheme: "Cookies",
    loginPath: "/cookies/login",
    configure: opts =>
    {
        opts.Cookie.Name = "Kestrun.Cookie";
        opts.Cookie.HttpOnly = true;
        opts.Cookie.SecurePolicy = CookieSecurePolicy.Always;
        opts.LoginPath = "/cookies/login";
        opts.LogoutPath = "/cookies/logout";
        opts.AccessDeniedPath = "/cookies/access-denied";
        opts.SlidingExpiration = true;
        opts.ExpireTimeSpan = TimeSpan.FromMinutes(60);
        opts.Cookie.SameSite = SameSiteMode.Strict;
    }
);

//***************************************************************************************
//    CERTIFICATE IMPORT/CREATION
//    ──────────────────────────────────────────
//    This section handles the creation or import of a self-signed certificate.
//****************************************************************************************
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



// 2. Configure Kestrel server
// This will set up the server to listen on specific ports and IP addresses
// It will also configure the server to use the provided certificate for HTTPS
server.ConfigureListener(
    port: 5001,
    ipAddress: IPAddress.Loopback,
    x509Certificate: x509Certificate,
    protocols: Microsoft.AspNetCore.Server.Kestrel.Core.HttpProtocols.Http1AndHttp2AndHttp3
);
/// Configure HTTP listener
/// This will set up the server to listen on a specific port and IP address
/// It will also configure the server to use HTTP/1.1 protocol
server.ConfigureListener(
    port: 5000,
    ipAddress: IPAddress.Loopback,
    protocols: Microsoft.AspNetCore.Server.Kestrel.Core.HttpProtocols.Http1
);

// 3. Enable configuration
// This will load configuration from appsettings.json and environment variables
// It will also enable Serilog configuration from appsettings.json
server.EnableConfiguration();


//***************************************************************************************
//    ROUTES
//    ──────────────────────────────────────────
//    These routes are protected by the authentication schemes defined above.
//    They will only be accessible if the user is authenticated.
//****************************************************************************************

//*********************************************
//    BASIC AUTHENTICATION ROUTES
//**********************************************
server.AddMapRoute("/secure/ps/hello", HttpVerb.Get, """
    if (-not $Context.User.Identity.IsAuthenticated) {
        Write-KrErrorResponse -Message "Access denied" -StatusCode 401
        return
    }

    $user = $Context.User.Identity.Name
    Write-KrTextResponse -InputObject "Welcome, $user! You are authenticated by PowerShell Code." -ContentType "text/plain"
""", ScriptLanguage.PowerShell, [BasicPowershellScheme]);

server.AddMapRoute("/secure/cs/hello", HttpVerb.Get, """
    if (-not $Context.User.Identity.IsAuthenticated) {
        Write-KrErrorResponse -Message "Access denied" -StatusCode 401
        return
    }

    $user = $Context.User.Identity.Name
    Write-KrTextResponse -InputObject "Welcome, $user! You are authenticated by C# Code." -ContentType "text/plain"
""", ScriptLanguage.PowerShell, [BasicCSharpScheme]);


server.AddMapRoute("/secure/vb/hello", HttpVerb.Get, """
    if (-not $Context.User.Identity.IsAuthenticated) {
        Write-KrErrorResponse -Message "Access denied" -StatusCode 401
        return
    }

    $user = $Context.User.Identity.Name
    Write-KrTextResponse -InputObject "Welcome, $user! You are authenticated by VB.NET Code." -ContentType "text/plain"
""", ScriptLanguage.PowerShell, [BasicVBNetScheme]);

server.AddMapRoute("/secure/native/hello", HttpVerb.Get, """
    if (-not $Context.User.Identity.IsAuthenticated) {
        Write-KrErrorResponse -Message "Access denied" -StatusCode 401
        return
    }

    $user = $Context.User.Identity.Name
    Write-KrTextResponse -InputObject "Welcome, $user! You are authenticated by Native C# code." -ContentType "text/plain"
""", ScriptLanguage.PowerShell, [BasicNativeScheme]);



/*
********************************************
    API KEY AUTHENTICATION ROUTES
*********************************************
*/
server.AddMapRoute("/secure/key/simple/hello", HttpVerb.Get, async (ctx) =>
{
    var user = ctx.User?.Identity?.Name;
    await ctx.Response.WriteTextResponseAsync($"Welcome, {user}! You are authenticated using simple key matching.", 200);

}, [ApiKeySimple]);


server.AddMapRoute("/secure/key/ps/hello", HttpVerb.Get, """
    if (-not $Context.User.Identity.IsAuthenticated) {
        Write-KrErrorResponse -Message "Access denied" -StatusCode 401
        return
    }

    $user = $Context.User.Identity.Name
    Write-KrTextResponse -InputObject "Welcome, $user! You are authenticated by PowerShell Code."
    
""", ScriptLanguage.PowerShell, [ApiKeyPowerShell]);


server.AddMapRoute("/secure/key/cs/hello", HttpVerb.Get, """
    if (!Context.User.Identity.IsAuthenticated)
    {
        Context.Response.WriteErrorResponse("Access denied", 401);
        return;
    }

    var user = Context.User.Identity.Name;
    Context.Response.WriteTextResponse($"Welcome, {user}! You are authenticated by C# code.", 200);
""", ScriptLanguage.CSharp, [ApiKeyCSharp]);

server.AddMapRoute("/secure/key/vb/hello", HttpVerb.Get, """
    if (!Context.User.Identity.IsAuthenticated)
    {
        Context.Response.WriteErrorResponse("Access denied", 401);
        return;
    }

    var user = Context.User.Identity.Name;
    Context.Response.WriteTextResponse($"Welcome, {user}! You are authenticated by VB.NET code.", 200);
""", ScriptLanguage.CSharp, [ApiKeyVBNet]);


server.AddMapRoute("/secure/jwt/hello", HttpVerb.Get, """
    if (!Context.User.Identity.IsAuthenticated)
    {
        Context.Response.WriteErrorResponse("Access denied", 401);
        return;
    }

    var user = Context.User.Identity.Name;
    Context.Response.WriteTextResponse($"Welcome, {user}! You are authenticated.", 200);
""", ScriptLanguage.CSharp, [JwtScheme]);


server.AddMapRoute("/token/renew", HttpVerb.Get, async (ctx) =>
{
    var token = await builderResult.RenewAsync(TimeSpan.FromHours(1));
    await ctx.Response.WriteJsonResponseAsync(new { access_token = token });

}, [JwtScheme]);


server.AddMapRoute("/token/new", HttpVerb.Get, async (ctx) =>
{
    try
    {
        var build = tokenBuilder.WithSubject("admin").Build();
        var token = build.Token();

        await ctx.Response.WriteJsonResponseAsync(new { access_token = token, token_type = "Bearer", ExpiresIn = build.Expires }); // 1 hour TTL
        Log.Information("Generated new JWT token: {Token}", token);
    }
    catch (Exception ex)
    {
        Log.Error(ex, "Failed to generate JWT token");
        await ctx.Response.WriteErrorResponseAsync("Internal Server Error", 500);
    }
}, [BasicNativeScheme]);


/*
********************************************
    Cookie authentication routes
*********************************************
*/
server.AddMapRoute("/cookies/login", HttpVerb.Get, async ctx =>
{
    await ctx.Response.WriteTextResponseAsync(@"
<!DOCTYPE html>
<html>
  <head>
    <meta charset='utf-8' />
    <title>Login</title>
  </head>
  <body>
    <h1>Login</h1>
    <form method='post' action='/cookies/login'>
      <label>
        Username:
        <input type='text' name='username' required />
      </label><br/>
      <label>
        Password:
        <input type='password' name='password' required />
      </label><br/>
      <button type='submit'>Log In</button>
    </form>
  </body>
</html>", statusCode: 200, contentType: "text/html; charset=UTF-8");
});
server.AddMapRoute("/cookies/login", HttpVerb.Post, async (ctx) =>
{
    var form = ctx.Request.Form;
    if (form == null)
    {
        await ctx.Response.WriteJsonResponseAsync(new { success = false, error = "Form data missing" });
        return;
    }
    var username = form["username"];
    var password = form["password"];
    if (username == "admin" && password == "secret")
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.Name, username)
        };
        var identity = new ClaimsIdentity(claims, "Cookies");
        var principal = new ClaimsPrincipal(identity);
        await ctx.HttpContext.SignInAsync("Cookies", principal);
        await ctx.Response.WriteJsonResponseAsync(new { success = true });
    }
    else
    {
        await ctx.Response.WriteJsonResponseAsync(new { success = false });
    }
});

server.AddMapRoute("/cookies/logout", HttpVerb.Get, async ctx =>
{
    await ctx.HttpContext.SignOutAsync("Cookies");
    // After logout, send them back to login
    ctx.Response.StatusCode = 302;
    ctx.Response.Headers["Location"] = "/cookies/login";
}, ["Cookies"]);

server.AddMapRoute("/cookies/login2", HttpVerb.Post, """
    write-host 'Processing login request...'
    # Check if the form contains the expected fields
    Write-KrInformationLog -MessageTemplate 'Received login request with form data: {@Position}' -PropertyValues $Context.Request.Form
    Write-Host "User: $($Context.Request.Form['user']), Password: $($Context.Request.Form['pass'])"
    if ($Context.Request.Form['user'] -eq 'admin' -and $Request.Form['pass'] -eq 'secret') {
        # Build claims principal
        $claims = [System.Collections.Generic.List[System.Security.Claims.Claim]]::new()
        $claims.Add([System.Security.Claims.Claim]::new('sub','admin'))
        $identity = [System.Security.Claims.ClaimsIdentity]::new($claims,'Cookies')
        $principal = [System.Security.Claims.ClaimsPrincipal]::new($identity)
        # Issue the cookie
        $Context.Request.HttpContext.SignInAsync('Cookies',$principal).Wait()
        Write-KrTextResponse -InputObject 'Logged in successfully!' -ContentType 'text/plain' 
    } else {
        Write-KrErrorResponse -Message 'Unauthorized' -StatusCode 401
    }
""", ScriptLanguage.PowerShell);

server.AddMapRoute("/cookies/secure2", HttpVerb.Get, """
    # Check if the user is authenticated
    if (!$Context.User.Identity.IsAuthenticated) {
        Write-KrRedirectResponse -Location '/cookies/login'
    } else {
        Write-KrTextResponse -InputObject ('Hello, '+$Context.User.Identity.Name) -ContentType 'text/plain'
    }
  """, ScriptLanguage.PowerShell, ["Cookies"]);

server.AddMapRoute("/cookies/secure3", HttpVerb.Get, """
   if (Context.User?.Identity == null || !Context.User.Identity.IsAuthenticated)
    {
        Context.Response.WriteRedirectResponse("/cookies/login");
    }
    else
    {
       await Context.Response.WriteTextResponseAsync($"Hello, {Context.User.Identity.Name}");
    }
""", ScriptLanguage.CSharp, ["Cookies"]);


server.AddMapRoute("/cookies/secure", HttpVerb.Get, async (ctx) =>
{
    if (ctx.User?.Identity == null || !ctx.User.Identity.IsAuthenticated)
    {
        ctx.Response.WriteRedirectResponse("/cookies/login");
    }
    else
    {
        await ctx.Response.WriteTextResponseAsync($"Hello, {ctx.User.Identity.Name}");
    }
}, ["Cookies"]);


//***************************************************************************************
//    Kerberos Authentication Route
//    ──────────────────────────────────────────
//    This route is protected by Kerberos authentication.
//    It will only be accessible if the user is authenticated via Kerberos.
//****************************************************************************************
server.AddMapRoute("/kerberos/secure", HttpVerb.Get, async (ctx) =>
{
    var userName = ctx.User?.Identity?.Name ?? "Unknown";
    await ctx.Response.WriteTextResponseAsync($"Hello, {userName}");
}, [NegotiateDefaults.AuthenticationScheme]);



//***************************************************************************************
//    Start the server
//    ──────────────────────────────────────────
//    This will start the Kestrun server and listen for incoming requests.
//****************************************************************************************

await server.RunUntilShutdownAsync(
  consoleEncoding: Encoding.UTF8,
  onStarted: () => Console.WriteLine("Server ready 🟢"),
  onShutdownError: ex => Console.WriteLine($"Shutdown error: {ex.Message}"

  )
);