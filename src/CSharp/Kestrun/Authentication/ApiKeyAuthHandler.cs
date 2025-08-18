using System.Management.Automation;
using System.Security.Claims;
using System.Text;
using System.Text.Encodings.Web;
using Kestrun.Hosting;
using Kestrun.Languages;
using Kestrun.Models;
using Kestrun.SharedState;
using Kestrun.Utilities;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Primitives;
using Org.BouncyCastle.Asn1.Iana;
using Serilog;

namespace Kestrun.Authentication;

/// <summary>
/// Handles API Key authentication for incoming HTTP requests.
/// </summary>
public class ApiKeyAuthHandler
    : AuthenticationHandler<ApiKeyAuthenticationOptions>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ApiKeyAuthHandler"/> class.
    /// </summary>
    /// <param name="options">The options monitor for API key authentication options.</param>
    /// <param name="logger">The logger factory.</param>
    /// <param name="encoder">The URL encoder.</param>
    public ApiKeyAuthHandler(
        IOptionsMonitor<ApiKeyAuthenticationOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder)
        : base(options, logger, encoder)
    {
        if (options.CurrentValue.Logger.IsEnabled(Serilog.Events.LogEventLevel.Debug))
        {
            options.CurrentValue.Logger.Debug("ApiKeyAuthHandler initialized");
        }
    }

    /// <summary>
    /// Authenticates the incoming request using an API key.
    /// </summary>
    /// <returns>An <see cref="AuthenticateResult"/> indicating the authentication outcome.</returns>
    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        try
        {
            if (Options.Logger.IsEnabled(Serilog.Events.LogEventLevel.Debug))
            {
                Options.Logger.Debug("Handling API Key authentication for request: {Request}", Request);
            }

            if (Options.RequireHttps && !Request.IsHttps)
            {
                return Fail("HTTPS required");
            }

            // ① Try the primary header
            if (!Request.Headers.TryGetValue(Options.HeaderName, out var values))
            {
                // ② Then try any additional headers
                foreach (var header in Options.AdditionalHeaderNames)
                {
                    if (Request.Headers.TryGetValue(header, out values))
                    {
                        break;
                    }
                }
            }

            // ③ Finally, if still missing & fallback is allowed, check the query string
            if ((values.Count == 0 || StringValues.IsNullOrEmpty(values))
                && Options.AllowQueryStringFallback
                && Request.Query.TryGetValue(Options.HeaderName, out var qsValues))
            {
                values = qsValues;
            }

            // ④ If we still have nothing, fail
            if (StringValues.IsNullOrEmpty(values))
            {
                return Fail("Missing API Key");
            }

            var providedKey = values.ToString();
            var providedKeyBytes = Encoding.UTF8.GetBytes(providedKey);
            // ⑤ Now validate
            bool valid = false;

            if (Options.ExpectedKeyBytes is not null)
            {
                valid = FixedTimeEquals.Test(providedKeyBytes, Options.ExpectedKeyBytes);
            }
            else if (Options.ValidateKeyAsync is not null)
            {
                valid = await Options.ValidateKeyAsync(Context, providedKey, providedKeyBytes);
            }
            else
            {
                throw new InvalidOperationException(
                    "No API key validation configured. " +
                    "Either set ValidateKey or ExpectedKey in ApiKeyAuthenticationOptions.");
            }

            if (!valid)
            {
                return Fail($"Invalid API Key: {providedKey}");
            }


            // If we reach here, the API key is valid
            var ticket = await IAuthHandler.GetAuthenticationTicketAsync(Context, providedKey, Options, Scheme, "ApiKeyClient");
            // Log the successful authentication
            if (ticket.Principal is null)
            {
                return Fail("Authentication ticket has no principal");
            }
            Options.Logger.Information("API Key authentication succeeded for identity: {Identity}", ticket.Principal.Identity?.Name ?? "Unknown");

            // ⑥ Return success
            return AuthenticateResult.Success(ticket);
        }
        catch (Exception ex)
        {
            Options.Logger.Error(ex, "API Key authentication failed with exception.");
            return Fail("Error processing API Key");
        }
    }
    AuthenticateResult Fail(string reason)
    {
        Options.Logger.Warning("API Key authentication failed: {Reason}", reason);
        return AuthenticateResult.Fail(reason);
    }

    /// <summary>
    /// Handles the authentication challenge by setting the appropriate response headers and status code.
    /// </summary>
    /// <param name="properties">Authentication properties for the challenge.</param>
    /// <returns>A completed task.</returns>
    protected override Task HandleChallengeAsync(AuthenticationProperties properties)
    {
        if (Options.EmitChallengeHeader)
        {
            var header = Options.HeaderName ?? "X-Api-Key";
            var value = Options.ChallengeHeaderFormat switch
            {
                ApiKeyChallengeFormat.HeaderOnly => header,
                _ => $"ApiKey header=\"{header}\""
            };

            Response.Headers.WWWAuthenticate = value;
        }
        Response.StatusCode = StatusCodes.Status401Unauthorized;
        return Task.CompletedTask;
    }
    /// <summary>
    /// Builds a PowerShell-based API key validator delegate using the provided authentication code settings.
    /// </summary>
    /// <param name="settings">The settings containing the PowerShell authentication code.</param>
    /// <param name="logger">The logger to use for debug output.</param>
    /// <returns>A delegate that validates an API key using PowerShell code.</returns>
    /// <remarks>
    ///  This method compiles the PowerShell script and returns a delegate that can be used to validate API keys.
    /// </remarks>
    public static Func<HttpContext, string, byte[], Task<bool>> BuildPsValidator(AuthenticationCodeSettings settings, Serilog.ILogger logger)
    {
        if (logger.IsEnabled(Serilog.Events.LogEventLevel.Debug))
        {
            logger.Debug("BuildPsValidator  settings: {Settings}", settings);
        }

        return async (ctx, providedKey, providedKeyBytes) =>
               {
                   return await IAuthHandler.ValidatePowerShellAsync(settings.Code, ctx, new Dictionary<string, string>
                   {
                    { "providedKey", providedKey }
                   },logger);
               };
    }

    /// <summary>
    /// Builds a C#-based API key validator delegate using the provided authentication code settings.
    /// </summary>
    /// <param name="settings">The settings containing the C# authentication code.</param>
    /// <param name="logger">The logger to use for debug output.</param>
    /// <returns>A delegate that validates an API key using C# code.</returns>
    public static Func<HttpContext, string, byte[], Task<bool>> BuildCsValidator(AuthenticationCodeSettings settings, Serilog.ILogger logger)
    {
        if (logger.IsEnabled(Serilog.Events.LogEventLevel.Debug))
        {
            logger.Debug("BuildCsValidator  settings: {Settings}", settings);
        }
        // pass the settings to the core C# validator
        var core = IAuthHandler.BuildCsValidator(
            settings,
            logger,
            ("providedKey", string.Empty), ("providedKeyBytes", Array.Empty<byte>())
            ) ?? throw new InvalidOperationException("Failed to build C# validator delegate from provided settings.");
        return (ctx, providedKey, providedKeyBytes) =>
            core(ctx, new Dictionary<string, object?>
            {
                ["providedKey"] = providedKey,
                ["providedKeyBytes"] = providedKeyBytes
            });
    }

    /// <summary>
    /// Builds a VB.NET-based API key validator delegate using the provided authentication code settings.
    /// </summary>
    /// <param name="settings">The settings containing the VB.NET authentication code.</param>
    /// <param name="logger">The logger to use for debug output.</param>
    /// <returns>A delegate that validates an API key using VB.NET code.</returns>
    public static Func<HttpContext, string, byte[], Task<bool>> BuildVBNetValidator(AuthenticationCodeSettings settings, Serilog.ILogger logger)
    {
        if (logger.IsEnabled(Serilog.Events.LogEventLevel.Debug))
        {
            logger.Debug("BuildVBNetValidator  settings: {Settings}", settings);
        }
        // pass the settings to the core VB.NET validator
        var core = IAuthHandler.BuildVBNetValidator(
            settings,
            logger,
            ("providedKey", string.Empty), ("providedKeyBytes", Array.Empty<byte>())
            ) ?? throw new InvalidOperationException("Failed to build VB.NET validator delegate from provided settings.");
        return (ctx, providedKey, providedKeyBytes) =>
            core(ctx, new Dictionary<string, object?>
            {
                ["providedKey"] = providedKey,
                ["providedKeyBytes"] = providedKeyBytes
            });
    }
}