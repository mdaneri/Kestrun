using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.Certificate;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;
using System.Text.Encodings.Web;
using System.Security.Claims;
using System.Net.Http.Headers;
using System.Text.RegularExpressions;


namespace Kestrun.Hosting;

public static class KestrunHostAuthExtensions
{


    public static KestrunHost AddBasicAuthentication<THandler>(
        this KestrunHost host,
        string scheme = "Basic",
        Action<AuthenticationSchemeOptions>? configure = null,   // new
        Action<AuthorizationOptions>? configureAuthz = null)
        where THandler : AuthenticationHandler<AuthenticationSchemeOptions>
    {
        return host.AddAuthentication(
            defaultScheme: scheme,
            buildSchemes: ab =>
            {
                ab.AddScheme<AuthenticationSchemeOptions, THandler>(
                    authenticationScheme: scheme,
                    displayName: "Basic Authentication",
                    configureOptions: configure ?? (_ => { })      // ✅ 3rd arg
                );
            },
            configureAuthz: configureAuthz);
    }


    /// <summary>
    /// Adds JWT Bearer authentication to the Kestrun host.
    /// <para>Use this for APIs that require token-based authentication.</para>
    /// </summary>
    /// <param name="host">The Kestrun host.</param>
    /// <param name="scheme">The authentication scheme.</param>
    /// <param name="issuer">The token issuer.</param>
    /// <param name="audience">The token audience.</param>
    /// <param name="validationKey">The key used to validate the token.</param>
    /// <param name="validAlgorithms">The valid algorithms for token validation.</param>
    /// <param name="configureAuthz"></param>
    /// <example>
    /// HS512 (HMAC-SHA-512, symmetric)
    /// </example>
    /// <code>
    ///     var hmacKey = new SymmetricSecurityKey(
    ///         Encoding.UTF8.GetBytes("32-bytes-or-more-secret……"));
    ///     host.AddJwtBearerAuthentication(
    ///         scheme:          "Bearer",
    ///         issuer:          "KestrunApi",
    ///         audience:        "KestrunClients",
    ///         validationKey:   hmacKey,
    ///         validAlgorithms: new[] { SecurityAlgorithms.HmacSha512 });
    /// </code>
    /// <example>
    /// RS256 (RSA-SHA-256, asymmetric)
    /// <para>Requires a PEM-encoded private key file.</para>
    /// <code>
    ///    using var rsa = RSA.Create();
    ///     rsa.ImportFromPem(File.ReadAllText("private-key.pem"));
    ///     var rsaKey = new RsaSecurityKey(rsa);
    ///
    ///     host.AddJwtBearerAuthentication(
    ///         scheme:          "Rs256",
    ///         issuer:          "KestrunApi",
    ///         audience:        "KestrunClients",
    ///         validationKey:   rsaKey,
    ///         validAlgorithms: new[] { SecurityAlgorithms.RsaSha256 });
    /// </code>
    /// </example>
    /// <example>
    /// ES256 (ECDSA-SHA-256, asymmetric)
    /// <para>Requires a PEM-encoded private key file.</para>
    /// <code>
    ///     using var ecdsa = ECDsa.Create(ECCurve.NamedCurves.nistP256);
    ///     var esKey = new ECDsaSecurityKey(ecdsa);
    ///     host.AddJwtBearerAuthentication(
    ///         "Es256", "KestrunApi", "KestrunClients",
    ///         esKey, new[] { SecurityAlgorithms.EcdsaSha256 });
    /// </code>
    /// </example>
    /// <returns></returns>
    public static KestrunHost AddJwtBearerAuthentication(
           this KestrunHost host,
           string scheme,
           string issuer,
           string audience,
           SecurityKey validationKey,          // ← any key type
           string[]? validAlgorithms = null,  // ← optional restriction
           Action<AuthorizationOptions>? configureAuthz = null)
    {
        return host.AddAuthentication(
            defaultScheme: scheme,
            buildSchemes: ab =>
            {
                ab.AddJwtBearer(
                    authenticationScheme: scheme,
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
                            IssuerSigningKey = validationKey,
                            ClockSkew = TimeSpan.FromMinutes(1),
                            // Accept only these algs if provided
                            ValidAlgorithms = validAlgorithms
                        };
                    });
            },
            configureAuthz: configureAuthz);
    }

    public static KestrunHost AddCookieAuthentication(
        this KestrunHost host,
        string scheme = CookieAuthenticationDefaults.AuthenticationScheme,
        string loginPath = "/account/login",
        Action<CookieAuthenticationOptions>? configure = null,
        Action<AuthorizationOptions>? configureAuthz = null)
    {
        return host.AddAuthentication(
            defaultScheme: scheme,
            buildSchemes: ab =>
            {
                ab.AddCookie(
                    authenticationScheme: scheme,
                    configureOptions: opts =>
                    {
                        opts.LoginPath = loginPath;
                        configure?.Invoke(opts);
                    });
            },
            configureAuthz: configureAuthz
        );
    }

    public static KestrunHost AddClientCertificateAuthentication(
        this KestrunHost host,
        string scheme = CertificateAuthenticationDefaults.AuthenticationScheme,
        Action<CertificateAuthenticationOptions>? configure = null,
        Action<AuthorizationOptions>? configureAuthz = null)
    {
        return host.AddAuthentication(
            defaultScheme: scheme,
            buildSchemes: ab =>
            {
                ab.AddCertificate(
                    authenticationScheme: scheme,
                    configureOptions: configure ?? (opts => { }));
            },
            configureAuthz: configureAuthz
        );
    }


    public static KestrunHost AddApiKeyAuthentication(
    this KestrunHost host,
    string scheme = "ApiKey",
    Action<ApiKeyAuthenticationOptions>? configure = null,
    Action<AuthorizationOptions>? configureAuthz = null)
    {
        return host.AddAuthentication(
            defaultScheme: scheme,
            buildSchemes: ab =>
            {
                // ← TOptions == ApiKeyAuthenticationOptions
                //    THandler == ApiKeyAuthHandler
                ab.AddScheme<ApiKeyAuthenticationOptions, ApiKeyAuthHandler>(
                    authenticationScheme: scheme,
                    displayName: "API Key",
                    configureOptions: configure ?? (_ => { })
                );
            },
            configureAuthz: configureAuthz
        );
    }



    public static KestrunHost AddOpenIdConnectAuthentication(
        this KestrunHost host,
        string scheme,
        string clientId,
        string clientSecret,
        string authority,
        Action<OpenIdConnectOptions>? configure = null,
        Action<AuthorizationOptions>? configureAuthz = null)
    {
        return host.AddAuthentication(
            defaultScheme: scheme,
            buildSchemes: ab =>
            {
                ab.AddOpenIdConnect(
                    authenticationScheme: scheme,
                    displayName: "OIDC",
                    configureOptions: opts =>
                    {
                        opts.ClientId = clientId;
                        opts.ClientSecret = clientSecret;
                        opts.Authority = authority;
                        opts.ResponseType = "code";
                        opts.SaveTokens = true;
                        configure?.Invoke(opts);
                    });
            },
            configureAuthz: configureAuthz
        );
    }
}

public class ApiKeyAuthHandler
    : AuthenticationHandler<ApiKeyAuthenticationOptions>
{
    public ApiKeyAuthHandler(
        IOptionsMonitor<ApiKeyAuthenticationOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder)
        : base(options, logger, encoder) { }

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        try
        {
            if (!Request.Headers.TryGetValue(Options.HeaderName, out var values))
                return Task.FromResult(AuthenticateResult.Fail("Missing API Key"));

            var apiKey = values.ToString();
            if (!Options.ValidateKey(apiKey))
                return Task.FromResult(AuthenticateResult.Fail("Invalid API Key"));

            var claims = new[] { new Claim(ClaimTypes.Name, "ApiKeyClient") };
            var identity = new ClaimsIdentity(claims, Scheme.Name);
            var principal = new ClaimsPrincipal(identity);
            var ticket = new AuthenticationTicket(principal, Scheme.Name);

            return Task.FromResult(AuthenticateResult.Success(ticket));
        }
        catch
        {
            return Task.FromResult(AuthenticateResult.Fail("Invalid Authorization Header"));
        }
    }
}

#region ApiKey Authentication

public class ApiKeyAuthenticationOptions : AuthenticationSchemeOptions
{
    /// <summary>HTTP header to read the key from.</summary>
    public string HeaderName { get; set; } = "X-Api-Key";

    /// <summary>Called to validate the raw key string. Return true if valid.</summary>
    public Func<string, bool> ValidateKey { get; set; } = _ => false;


    public Serilog.ILogger Logger { get; set; } = Serilog.Log.ForContext<ApiKeyAuthenticationOptions>();
}
#endregion

#region Basic Authentication


public partial class BasicAuthenticationOptions : AuthenticationSchemeOptions
{
    /// <summary>HTTP header to read the key from.</summary>
    /// <remarks>Defaults to "Authorization".</remarks>
    public string HeaderName { get; set; } = "Authorization";

    /// <summary>Called to validate the raw key string. Return true if valid.</summary>
    public Func<string[], bool> ValidateCredentials { get; set; } = _ => false;

    public bool Base64Encoded { get; set; } = true;

    public Regex SeparatorRegex { get; set; } = MyRegex();


    public string? Realm { get; set; } = "Kestrun";

    [GeneratedRegex("^(.+?):(.+)$", RegexOptions.Compiled)]
    private static partial Regex MyRegex();

    public Serilog.ILogger Logger { get; set; } = Serilog.Log.ForContext<BasicAuthenticationOptions>();
}


public class BasicAuthHandler : AuthenticationHandler<BasicAuthenticationOptions>
{
    public BasicAuthHandler(
        IOptionsMonitor<BasicAuthenticationOptions> options,
        ILoggerFactory loggerFactory,
        UrlEncoder encoder)
        : base(options, loggerFactory, encoder) { }
    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        try
        {
            if (!Request.Headers.TryGetValue(Options.HeaderName, out var authHeaderVal))
                return Fail("Missing Authorization Header");

            var authHeader = AuthenticationHeaderValue.Parse(authHeaderVal.ToString());
            if (Options.Base64Encoded && !string.Equals(authHeader.Scheme, "Basic", StringComparison.OrdinalIgnoreCase))
                return Fail("Invalid Authorization Scheme");

            var rawCreds = Options.Base64Encoded
                ? Encoding.UTF8.GetString(Convert.FromBase64String(authHeader.Parameter ?? ""))
                : authHeader.Parameter ?? "";

            // Use the regex match to extract exactly two groups:
            var match = Options.SeparatorRegex.Match(rawCreds);
            if (!match.Success || match.Groups.Count < 3)
                return Fail("Malformed credentials");

            // Group[1] is username, Group[2] is password:
            var user = match.Groups[1].Value;
            var pass = match.Groups[2].Value;

            if (!Options.ValidateCredentials([user, pass]))
                return Fail("Invalid credentials");

            var claims = new[] { new Claim(ClaimTypes.Name, user) };
            var identity = new ClaimsIdentity(claims, Scheme.Name);
            var principal = new ClaimsPrincipal(identity);
            var ticket = new AuthenticationTicket(principal, Scheme.Name);

            return Task.FromResult(AuthenticateResult.Success(ticket));
        }
        catch (Exception ex)
        {
            Options.Logger.Error(ex, "Exception in BasicAuthHandler");
            return Fail("Exception during authentication");
        }

        Task<AuthenticateResult> Fail(string reason)
        {
            Options.Logger.Warning("Basic auth failed: {Reason}", reason);
            return Task.FromResult(AuthenticateResult.Fail(reason));
        }
    }

    protected override Task HandleChallengeAsync(AuthenticationProperties properties)
    {
        var realm = Options.Realm ?? "Kestrun";
        Response.Headers.WWWAuthenticate = $"Basic realm=\"{realm}\", charset=\"UTF-8\"";
        Response.StatusCode = StatusCodes.Status401Unauthorized;
        return Task.CompletedTask;
    }
    
    protected override Task HandleForbiddenAsync(AuthenticationProperties properties)
    {
        Response.StatusCode = StatusCodes.Status403Forbidden;
        return Task.CompletedTask;
    }


    #endregion
}