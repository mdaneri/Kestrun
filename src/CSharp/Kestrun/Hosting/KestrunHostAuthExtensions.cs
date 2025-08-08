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
using Microsoft.AspNetCore.Authentication.Negotiate;
using Kestrun.Claims;


namespace Kestrun.Hosting;

/// <summary>
/// Provides extension methods for adding authentication and authorization schemes to the Kestrun host.
/// </summary>
public static class KestrunHostAuthExtensions
{
    /// <summary>
    /// Adds Basic Authentication to the Kestrun host.
    /// <para>Use this for simple username/password authentication.</para>
    /// </summary>
    /// <param name="host">The Kestrun host instance.</param>
    /// <param name="scheme">The authentication scheme name (e.g. "Basic").</param>
    /// <param name="configure">Optional configuration for BasicAuthenticationOptions.</param>
    /// <returns>returns the KestrunHost instance.</returns>
    public static KestrunHost AddBasicAuthentication(
    this KestrunHost host,
    string scheme = "Basic",
    Action<BasicAuthenticationOptions>? configure = null
    )
    {
        var h = host.AddAuthentication(
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
                       if (opts.ValidateCodeSettings.Language == ScriptLanguage.PowerShell &&
                           !string.IsNullOrWhiteSpace(opts.ValidateCodeSettings.Code))
                       {
                           // Build the PowerShell script validator
                           // This will be used to validate credentials
                           opts.ValidateCredentialsAsync = BasicAuthHandler.BuildPsValidator(opts.ValidateCodeSettings);
                       }
                       else   // ── C# pathway ─────────────────────────────────
                       if (opts.ValidateCodeSettings.Language is ScriptLanguage.CSharp
                           && !string.IsNullOrWhiteSpace(opts.ValidateCodeSettings.Code))
                       {
                           // Build the C# script validator
                           // This will be used to validate credentials
                           opts.ValidateCredentialsAsync = BasicAuthHandler.BuildCsValidator(opts.ValidateCodeSettings);
                       }
                       else
                         if (opts.ValidateCodeSettings.Language is ScriptLanguage.VBNet
                           && !string.IsNullOrWhiteSpace(opts.ValidateCodeSettings.Code))
                       {
                           // Build the VB.NET script validator
                           // This will be used to validate credentials
                           opts.ValidateCredentialsAsync = BasicAuthHandler.BuildVBNetValidator(opts.ValidateCodeSettings);
                       }

                       // ── SPECIAL POWER-SHELL PATH ────────────────────
                       // If the IssueClaimsCodeSettings is set to PowerShell, we build the PowerShell
                       // script validator
                       if (opts.IssueClaimsCodeSettings.Language == ScriptLanguage.PowerShell &&
                          !string.IsNullOrWhiteSpace(opts.IssueClaimsCodeSettings.Code))
                       {
                           opts.IssueClaims = IAuthHandler.BuildPsIssueClaims(opts.IssueClaimsCodeSettings);
                       }
                       else   // ── C# pathway ─────────────────────────────────
                      if (opts.IssueClaimsCodeSettings.Language is ScriptLanguage.CSharp
                          && !string.IsNullOrWhiteSpace(opts.IssueClaimsCodeSettings.Code))
                       {
                           opts.IssueClaims = IAuthHandler.BuildCsIssueClaims(opts.IssueClaimsCodeSettings);
                       }
                       else
                        if (opts.IssueClaimsCodeSettings.Language is ScriptLanguage.VBNet
                          && !string.IsNullOrWhiteSpace(opts.IssueClaimsCodeSettings.Code))
                       {
                           opts.IssueClaims = IAuthHandler.BuildVBNetIssueClaims(opts.IssueClaimsCodeSettings);
                       }
                   });
           }
       );
        //  register the post-configurer **after** the scheme so it can
        //    read BasicAuthenticationOptions for <scheme>
        return h.AddService(services =>
        {
            services.AddSingleton<IPostConfigureOptions<AuthorizationOptions>>(
                sp => new ClaimPolicyPostConfigurer(
                          scheme,
                          sp.GetRequiredService<
                              IOptionsMonitor<BasicAuthenticationOptions>>()));
        });
    }
    /// <summary>
    /// Adds Basic Authentication to the Kestrun host using the provided options object.
    /// </summary>
    /// <param name="host">The Kestrun host instance.</param>
    /// <param name="scheme">The authentication scheme name (e.g. "Basic").</param>
    /// <param name="configure">The BasicAuthenticationOptions object to configure the authentication.</param>
    /// <returns>The configured KestrunHost instance.</returns>
    public static KestrunHost AddBasicAuthentication(
        this KestrunHost host,
        string scheme,
        BasicAuthenticationOptions configure
        )
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
                opts.ValidateCodeSettings = configure.ValidateCodeSettings;
                opts.IssueClaimsCodeSettings = configure.IssueClaimsCodeSettings;

                // Claims policy configuration
                opts.ClaimPolicyConfig = configure.ClaimPolicyConfig;
            }
        );

    }


    /// <summary>
    /// Adds JWT Bearer authentication to the Kestrun host.
    /// <para>Use this for APIs that require token-based authentication.</para>
    /// </summary>
    /// <param name="host">The Kestrun host instance.</param>
    /// <param name="scheme">The authentication scheme name (e.g. "Bearer").</param>
    /// <param name="validationParameters">Parameters used to validate JWT tokens.</param>
    /// <param name="configureJwt">Optional hook to customize JwtBearerOptions.</param>
    /// <param name="claimPolicy">Optional authorization policy configuration.</param>
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
      TokenValidationParameters validationParameters,
      Action<JwtBearerOptions>? configureJwt = null,
      ClaimPolicyConfig? claimPolicy = null)
    {
        return host.AddAuthentication(
            defaultScheme: scheme,
            buildSchemes: ab =>
            {
                ab.AddJwtBearer(scheme, opts =>
                {
                    opts.TokenValidationParameters = validationParameters;
                    configureJwt?.Invoke(opts);
                });
            },
            configureAuthz: claimPolicy?.ToAuthzDelegate()
            );

    }

    /// <summary>
    /// Adds Cookie Authentication to the Kestrun host.
    /// <para>Use this for browser-based authentication using cookies.</para>
    /// </summary>
    /// <param name="host">The Kestrun host instance.</param>
    /// <param name="scheme">The authentication scheme name (default is CookieAuthenticationDefaults.AuthenticationScheme).</param>    
    /// <param name="configure">Optional configuration for CookieAuthenticationOptions.</param>
    /// <param name="claimPolicy">Optional authorization policy configuration.</param>
    /// <returns>The configured KestrunHost instance.</returns>
    public static KestrunHost AddCookieAuthentication(
        this KestrunHost host,
        string scheme = CookieAuthenticationDefaults.AuthenticationScheme,
        Action<CookieAuthenticationOptions>? configure = null,
     ClaimPolicyConfig? claimPolicy = null)
    {
        return host.AddAuthentication(
            defaultScheme: scheme,
            buildSchemes: ab =>
            {
                ab.AddCookie(
                    authenticationScheme: scheme,
                    configureOptions: opts =>
                    {
                        configure?.Invoke(opts);
                    });
            },
             configureAuthz: claimPolicy?.ToAuthzDelegate()
        );
    }


    /// <summary>
    /// Adds Cookie Authentication to the Kestrun host using the provided options object.
    /// </summary>
    /// <param name="host">The Kestrun host instance.</param>
    /// <param name="scheme">The authentication scheme name (default is CookieAuthenticationDefaults.AuthenticationScheme).</param>
    /// <param name="configure">The CookieAuthenticationOptions object to configure the authentication.</param>
    /// <param name="claimPolicy">Optional authorization policy configuration.</param>
    /// <returns>The configured KestrunHost instance.</returns>
        public static KestrunHost AddCookieAuthentication(
              this KestrunHost host,
              string scheme = CookieAuthenticationDefaults.AuthenticationScheme,
              CookieAuthenticationOptions? configure = null,
           ClaimPolicyConfig? claimPolicy = null)
        {
            return host.AddCookieAuthentication(
                scheme: scheme,
                configure: opts =>
                {
                    opts = configure ?? new CookieAuthenticationOptions();
    
                },
                 claimPolicy: claimPolicy
            );
        }


    /*
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
    */
    /// <summary>
    /// Adds Windows Authentication to the Kestrun host.
    /// <para>
    /// The authentication scheme name is <see cref="NegotiateDefaults.AuthenticationScheme"/>.
    /// This enables Kerberos and NTLM authentication.
    /// </para>
    /// </summary>
    /// <param name="host">The Kestrun host instance.</param>
    /// <returns>The configured KestrunHost instance.</returns>
    public static KestrunHost AddWindowsAuthentication(this KestrunHost host)
    {
        return host.AddAuthentication(
            defaultScheme: NegotiateDefaults.AuthenticationScheme,
            buildSchemes: ab =>
            {
                ab.AddNegotiate();
            }
        );
    }
    /// <summary>
    /// Adds API Key Authentication to the Kestrun host.
    /// <para>Use this for endpoints that require an API key for access.</para>
    /// </summary>
    /// <param name="host">The Kestrun host instance.</param>
    /// <param name="scheme">The authentication scheme name (default is "ApiKey").</param>
    /// <param name="configure">Optional configuration for ApiKeyAuthenticationOptions.</param>
    /// <returns>The configured KestrunHost instance.</returns>
    public static KestrunHost AddApiKeyAuthentication(
    this KestrunHost host,
    string scheme = "ApiKey",
    Action<ApiKeyAuthenticationOptions>? configure = null)
    {
        var h = host.AddAuthentication(
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
                       if (opts.ValidateCodeSettings.Language == ScriptLanguage.PowerShell &&
                           !string.IsNullOrWhiteSpace(opts.ValidateCodeSettings.Code))
                       {
                           // Build the PowerShell script validator
                           // This will be used to validate credentials
                           opts.ValidateKeyAsync = ApiKeyAuthHandler.BuildPsValidator(opts.ValidateCodeSettings);
                       }
                       else   // ── C# pathway ─────────────────────────────────
                       if (opts.ValidateCodeSettings.Language is ScriptLanguage.CSharp
                           && !string.IsNullOrWhiteSpace(opts.ValidateCodeSettings.Code))
                       {
                           // Build the C# script validator
                           // This will be used to validate credentials
                           opts.ValidateKeyAsync = ApiKeyAuthHandler.BuildCsValidator(opts.ValidateCodeSettings);
                       }
                       else   // ── VB.NET pathway ─────────────────────────────────
                       if (opts.ValidateCodeSettings.Language is ScriptLanguage.VBNet
                           && !string.IsNullOrWhiteSpace(opts.ValidateCodeSettings.Code))
                       {
                           // Build the VB.NET script validator
                           // This will be used to validate credentials
                           opts.ValidateKeyAsync = ApiKeyAuthHandler.BuildVBNetValidator(opts.ValidateCodeSettings);
                       }

                       // ── SPECIAL POWER-SHELL PATH ────────────────────
                       // If the IssueClaimsCodeSettings is set to PowerShell, we build the PowerShell
                       // script validator
                       if (opts.IssueClaimsCodeSettings.Language == ScriptLanguage.PowerShell &&
                           !string.IsNullOrWhiteSpace(opts.IssueClaimsCodeSettings.Code))
                       {
                           opts.IssueClaims = IAuthHandler.BuildPsIssueClaims(opts.IssueClaimsCodeSettings);
                       }
                       else   // ── C# pathway ─────────────────────────────────
                       if (opts.IssueClaimsCodeSettings.Language is ScriptLanguage.CSharp
                           && !string.IsNullOrWhiteSpace(opts.IssueClaimsCodeSettings.Code))
                       {
                           opts.IssueClaims = IAuthHandler.BuildCsIssueClaims(opts.IssueClaimsCodeSettings);
                       }
                       else
                         if (opts.IssueClaimsCodeSettings.Language is ScriptLanguage.VBNet
                           && !string.IsNullOrWhiteSpace(opts.IssueClaimsCodeSettings.Code))
                       {
                           opts.IssueClaims = IAuthHandler.BuildVBNetIssueClaims(opts.IssueClaimsCodeSettings);
                       }
                   });
           }
       );
        //  register the post-configurer **after** the scheme so it can
        //    read BasicAuthenticationOptions for <scheme>
        return h.AddService(services =>
        {
            services.AddSingleton<IPostConfigureOptions<AuthorizationOptions>>(
                sp => new ClaimPolicyPostConfigurer(
                          scheme,
                          sp.GetRequiredService<
                              IOptionsMonitor<BasicAuthenticationOptions>>()));
        });
    }


    /// <summary>
    /// Adds API Key Authentication to the Kestrun host using the provided options object.
    /// </summary>
    /// <param name="host">The Kestrun host instance.</param>
    /// <param name="scheme">The authentication scheme name.</param>
    /// <param name="configure">The ApiKeyAuthenticationOptions object to configure the authentication.</param> 
    /// <returns>The configured KestrunHost instance.</returns>
    public static KestrunHost AddApiKeyAuthentication(
    this KestrunHost host,
    string scheme,
    ApiKeyAuthenticationOptions configure)
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
                opts.ValidateCodeSettings = configure.ValidateCodeSettings;
                // IssueClaimsCodeSettings
                opts.IssueClaimsCodeSettings = configure.IssueClaimsCodeSettings;
                // Claims policy configuration 
                opts.ClaimPolicyConfig = configure.ClaimPolicyConfig;
            }
        );
    }

    /// <summary>
    /// Adds OpenID Connect authentication to the Kestrun host.
    /// <para>Use this for applications that require OpenID Connect authentication.</para>
    /// </summary>
    /// <param name="host">The Kestrun host instance.</param>
    /// <param name="scheme">The authentication scheme name.</param>
    /// <param name="clientId">The client ID for the OpenID Connect application.</param>
    /// <param name="clientSecret">The client secret for the OpenID Connect application.</param>
    /// <param name="authority">The authority URL for the OpenID Connect provider.</param>
    /// <param name="configure">An optional action to configure the OpenID Connect options.</param>
    /// <param name="configureAuthz">An optional action to configure the authorization options.</param>
    /// <returns>The configured KestrunHost instance.</returns>
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


    /// <summary>
    /// Adds authentication and authorization middleware to the Kestrun host.
    /// </summary>
    /// <param name="host">The Kestrun host instance.</param>
    /// <param name="buildSchemes">A delegate to configure authentication schemes.</param>
    /// <param name="defaultScheme">The default authentication scheme (default is JwtBearer).</param>
    /// <param name="configureAuthz">Optional authorization policy configuration.</param>
    /// <returns>The configured KestrunHost instance.</returns>
    internal static KestrunHost AddAuthentication(this KestrunHost host,
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


    /// <summary>
    /// Adds authorization services to the Kestrun host.
    /// </summary>
    /// <param name="host">The Kestrun host instance.</param>
    /// <param name="cfg">Optional configuration for authorization options.</param>
    /// <returns>The configured KestrunHost instance.</returns>
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
