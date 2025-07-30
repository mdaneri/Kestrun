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
using Microsoft.Extensions.Primitives;
using Kestrun.Utilities;
using Kestrun.Authentication;
using Serilog.Events;
using Kestrun.Scripting;


namespace Kestrun.Hosting;

public static class KestrunHostAuthExtensions
{
    /// <summary>
    /// Adds Basic Authentication to the Kestrun host.
    /// <para>Use this for simple username/password authentication.</para>
    /// </summary>
    /// <param name="host">The Kestrun host instance.</param>
    /// <param name="scheme">The authentication scheme name (e.g. "Basic").</param>
    /// <param name="configure">Optional configuration for BasicAuthenticationOptions.</param>
    /// <param name="configureAuthz">Optional authorization policy configuration.</param>
    /// <returns></returns>
    public static KestrunHost AddBasicAuthentication(
    this KestrunHost host,
    string scheme = "Basic",
    Action<BasicAuthenticationOptions>? configure = null,
    Action<AuthorizationOptions>? configureAuthz = null)
    {
        return host.AddAuthentication(
            defaultScheme: scheme,
            buildSchemes: ab =>
            {
                // ← TOptions == BasicAuthenticationOptions
                //    THandler == BasicAuthHandler
                ab.AddScheme<BasicAuthenticationOptions, BasicAuthHandler>(
                    authenticationScheme: scheme,
                    displayName: "Basic Authentication",
                     configureOptions: opts =>
                    {
                        // let caller mutate everything first
                        configure?.Invoke(opts);

                        // ── SPECIAL POWER-SHELL PATH ────────────────────
                        if (opts.CodeSettings.Language == ScriptLanguage.PowerShell &&
                            !string.IsNullOrWhiteSpace(opts.CodeSettings.Code))
                        {
                            // Build the PowerShell script validator
                            // This will be used to validate credentials
                            opts.ValidateCredentials = BasicAuthHandler.BuildPsValidator(opts.CodeSettings);
                        }
                        else   // ── C# pathway ─────────────────────────────────
                        if (opts.CodeSettings.Language is ScriptLanguage.CSharp
                            && !string.IsNullOrWhiteSpace(opts.CodeSettings.Code))
                        {
                            // Build the C# script validator
                            // This will be used to validate credentials
                            opts.ValidateCredentials = BasicAuthHandler.BuildCsValidator(opts.CodeSettings);
                        }
                    });

            },
            configureAuthz: configureAuthz
        );
    }
    public static KestrunHost AddBasicAuthentication(
        this KestrunHost host,
        string scheme,
        BasicAuthenticationOptions configure,
        Action<AuthorizationOptions>? configureAuthz = null)
    {
        if (host._Logger.IsEnabled(LogEventLevel.Debug))
            host._Logger.Debug("Adding Basic Authentication with scheme: {Scheme}", scheme);
        // Ensure the scheme is not null
        ArgumentNullException.ThrowIfNull(host);
        ArgumentNullException.ThrowIfNull(scheme);
        ArgumentNullException.ThrowIfNull(configure);
        return host.AddBasicAuthentication(
            scheme: scheme,
            configure: opts =>
            {
                // Copy properties from the provided configure object                
                opts.HeaderName = configure.HeaderName;
                opts.Base64Encoded = configure.Base64Encoded;
                if (configure.SeparatorRegex is not null)
                    opts.SeparatorRegex = new Regex(configure.SeparatorRegex.ToString(), configure.SeparatorRegex.Options);
                opts.Realm = configure.Realm;
                opts.RequireHttps = configure.RequireHttps;
                opts.SuppressWwwAuthenticate = configure.SuppressWwwAuthenticate;
                opts.Logger = configure.Logger;
                // Copy properties from the provided configure object
                opts.CodeSettings = configure.CodeSettings;
                // ── SPECIAL POWER-SHELL PATH ────────────────────
                if (opts.CodeSettings.Language == ScriptLanguage.PowerShell &&
                    !string.IsNullOrWhiteSpace(opts.CodeSettings.Code))
                {
                    // Build the PowerShell script validator
                    // This will be used to validate credentials
                    opts.ValidateCredentials = BasicAuthHandler.BuildPsValidator(opts.CodeSettings);
                }
                else   // ── C# pathway ─────────────────────────────────
                if (opts.CodeSettings.Language is ScriptLanguage.CSharp
                    && !string.IsNullOrWhiteSpace(opts.CodeSettings.Code))
                {
                    // Build the C# script validator
                    // This will be used to validate credentials
                    opts.ValidateCredentials = BasicAuthHandler.BuildCsValidator(opts.CodeSettings);
                }
            },
            configureAuthz: configureAuthz
        );

    }


    /// <summary>
    /// Adds JWT Bearer authentication to the Kestrun host.
    /// <para>Use this for APIs that require token-based authentication.</para>
    /// </summary>
    /// <param name="scheme">The authentication scheme name (e.g. "Bearer").</param>
    /// <param name="issuer">Expected token issuer. Set to null to disable issuer validation.</param>
    /// <param name="audience">Expected token audience. Set to null to disable audience validation.</param>
    /// <param name="validationKey">Signing key: HMAC, RSA, or ECDSA.</param>
    /// <param name="validAlgorithms">List of accepted JWT signing algorithms (e.g. RS256).</param>
    /// <param name="validateIssuer">If true, requires issuer to match.</param>
    /// <param name="validateAudience">If true, requires audience to match.</param>
    /// <param name="validateLifetime">If true, checks token expiration.</param>
    /// <param name="validateSigningKey">If true, checks the signing key.</param
    /// <param name="clockSkew">Optional time window to allow clock drift. Default is 1 minute.</param>
    /// <param name="configureJwt">Optional hook to customize JwtBearerOptions.</param>
    /// <param name="configureAuthz">Optional authorization policy configuration.</param>
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
      /* string? issuer,
       string? audience,
       SecurityKey validationKey,
       string[]? validAlgorithms = null,
       bool validateIssuer = true,
       bool validateAudience = true,
       bool validateLifetime = true,
       bool validateSigningKey = true,
       TimeSpan? clockSkew = null,*/
      TokenValidationParameters validationParameters,
      Action<JwtBearerOptions>? configureJwt = null,
      Action<AuthorizationOptions>? configureAuthz = null)
    {
        return host.AddAuthentication(
            defaultScheme: scheme,
            buildSchemes: ab =>
            {
                ab.AddJwtBearer(scheme, opts =>
                {
                    /* opts.TokenValidationParameters = new TokenValidationParameters
                     {
                         ValidateIssuer = validateIssuer,
                         ValidIssuer = issuer,

                         ValidateAudience = validateAudience,
                         ValidAudience = audience,

                         ValidateLifetime = validateLifetime,
                         ValidateIssuerSigningKey = validateSigningKey,
                         IssuerSigningKey = validationKey,
                         ClockSkew = clockSkew ?? TimeSpan.FromMinutes(1),

                         ValidAlgorithms = validAlgorithms
                     };*/
                    opts.TokenValidationParameters = validationParameters;

                    configureJwt?.Invoke(opts);
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
                    configureOptions: opts =>
                    {
                        // let caller mutate everything first
                        configure?.Invoke(opts);

                        // ── SPECIAL POWER-SHELL PATH ────────────────────
                        if (opts.CodeSettings.Language == ScriptLanguage.PowerShell &&
                            !string.IsNullOrWhiteSpace(opts.CodeSettings.Code))
                        {
                            // Build the PowerShell script validator
                            // This will be used to validate credentials
                            opts.ValidateKeyAsync = ApiKeyAuthHandler.BuildPsValidator(opts.CodeSettings);
                        }
                        else   // ── C# pathway ─────────────────────────────────
                        if (opts.CodeSettings.Language is ScriptLanguage.CSharp
                            && !string.IsNullOrWhiteSpace(opts.CodeSettings.Code))
                        {
                            // Build the C# script validator
                            // This will be used to validate credentials
                            opts.ValidateKeyAsync = ApiKeyAuthHandler.BuildCsValidator(opts.CodeSettings);
                        }
                    });

            },
            configureAuthz: configureAuthz
        );
    }


    public static KestrunHost AddApiKeyAuthentication(
    this KestrunHost host,
    string scheme,
    ApiKeyAuthenticationOptions configure,
    Action<AuthorizationOptions>? configureAuthz = null)
    {
        if (host._Logger.IsEnabled(LogEventLevel.Debug))
            host._Logger.Debug("Adding API Key Authentication with scheme: {Scheme}", scheme);
        ArgumentNullException.ThrowIfNull(host);
        ArgumentNullException.ThrowIfNull(scheme);
        ArgumentNullException.ThrowIfNull(configure);
        return host.AddApiKeyAuthentication(
            scheme: scheme,
            configure: opts =>
            {
                // let caller mutate everything first
                opts.ExpectedKey = configure.ExpectedKey;
                opts.HeaderName = configure.HeaderName;
                opts.AdditionalHeaderNames = configure.AdditionalHeaderNames;
                opts.AllowQueryStringFallback = configure.AllowQueryStringFallback;
                opts.Logger = configure.Logger;
                opts.RequireHttps = configure.RequireHttps;
                opts.EmitChallengeHeader = configure.EmitChallengeHeader;
                opts.ChallengeHeaderFormat = configure.ChallengeHeaderFormat;
                opts.CodeSettings = configure.CodeSettings;
                // ── SPECIAL POWER-SHELL PATH ────────────────────
                if (opts.CodeSettings.Language == ScriptLanguage.PowerShell &&
                    !string.IsNullOrWhiteSpace(opts.CodeSettings.Code))
                {
                    // Build the PowerShell script validator
                    // This will be used to validate credentials
                    opts.ValidateKeyAsync = ApiKeyAuthHandler.BuildPsValidator(opts.CodeSettings);
                }
                else   // ── C# pathway ─────────────────────────────────
                if (opts.CodeSettings.Language is ScriptLanguage.CSharp
                    && !string.IsNullOrWhiteSpace(opts.CodeSettings.Code))
                {
                    // Build the C# script validator
                    // This will be used to validate credentials
                    opts.ValidateKeyAsync = ApiKeyAuthHandler.BuildCsValidator(opts.CodeSettings);
                }


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


    public static KestrunHost AddAuthentication(this KestrunHost host,
          string defaultScheme,
          Action<AuthenticationBuilder> buildPolicy, Action<AuthorizationOptions>? configureAuthz = null)
    {

        if (host._Logger.IsEnabled(LogEventLevel.Debug))
            host._Logger.Debug("Adding authentication with default scheme: {DefaultScheme}", defaultScheme);
        ArgumentNullException.ThrowIfNull(defaultScheme);
        ArgumentNullException.ThrowIfNull(buildPolicy);

        // ① Add authentication services via DI
        host.AddService(services =>
        {
            var builder = services.AddAuthentication(defaultScheme);
            buildPolicy(builder);  // ⬅️ Now you apply the user-supplied schemes here

            if (configureAuthz is null)
                services.AddAuthorization();                // default options
            else
                services.AddAuthorization(configureAuthz);  // caller customises
        });

        // ② Add middleware to enable auth pipeline
        return host.Use(app =>
        {
            app.UseAuthentication();
            app.UseAuthorization(); // optional but useful
        });
    }

    public static KestrunHost AddAuthentication(this KestrunHost host,
    Action<AuthenticationBuilder> buildSchemes,            // ← unchanged
    string defaultScheme = JwtBearerDefaults.AuthenticationScheme,
    Action<AuthorizationOptions>? configureAuthz = null)
    {
        host.AddService(services =>
        {
            var ab = services.AddAuthentication(defaultScheme);
            buildSchemes(ab);                                  // Basic + JWT here

            // make sure UseAuthorization() can find its services
            if (configureAuthz is null)
                services.AddAuthorization();
            else
                services.AddAuthorization(configureAuthz);
        });

        return host.Use(app =>
        {
            app.UseAuthentication();
            app.UseAuthorization();
        });
    }


    public static KestrunHost AddAuthorization(this KestrunHost host, Action<AuthorizationOptions>? cfg = null)
    {
        return host.AddService(s =>
        {
            if (cfg == null)
                s.AddAuthorization();
            else
                s.AddAuthorization(cfg);
        });
    }


}
