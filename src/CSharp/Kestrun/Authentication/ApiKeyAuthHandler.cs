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
        : base(options, logger, encoder) { }

    /// <summary>
    /// Authenticates the incoming request using an API key.
    /// </summary>
    /// <returns>An <see cref="AuthenticateResult"/> indicating the authentication outcome.</returns>
    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        try
        {
            if (Options.RequireHttps && !Request.IsHttps)
                return Fail("HTTPS required");

            // ① Try the primary header
            if (!Request.Headers.TryGetValue(Options.HeaderName, out var values))
            {
                // ② Then try any additional headers
                foreach (var header in Options.AdditionalHeaderNames)
                {
                    if (Request.Headers.TryGetValue(header, out values))
                        break;
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
                return Fail("Missing API Key");

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
            var identity = Options.ResolveUsername?.Invoke(providedKey) ?? "ApiKeyClient";
            
            // If we reach here, the API key is valid
            var ticket = await IAuthHandler.GetAuthenticationTicketAsync(Context, identity, Options, Scheme);
            // Log the successful authentication
            if (ticket.Principal is null)
            {
                return Fail("Authentication ticket has no principal");
            }
            Options.Logger.Information("API Key authentication succeeded for identity: {Identity}", identity);

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
    /// Validates the provided API key using a PowerShell script.
    /// </summary>
    /// <param name="code">The PowerShell script code to execute for validation.</param>
    /// <param name="context">The current HTTP context containing the PowerShell runspace.</param>
    /// <param name="providedKey">The API key to validate.</param>
    /// <returns>A <see cref="ValueTask{Boolean}"/> indicating whether the API key is valid.</returns>
    public static async ValueTask<bool> ValidatePowerShellKeyAsync(string? code, HttpContext context, string providedKey)
    {
        try
        {
            if (!context.Items.ContainsKey("PS_INSTANCE"))
            {
                throw new InvalidOperationException("PowerShell runspace not found in context items. Ensure PowerShellRunspaceMiddleware is registered.");
            }

            if (string.IsNullOrWhiteSpace(providedKey))
            {
                Log.Warning("API Key is null or empty.");
                return false;
            }
            if (string.IsNullOrEmpty(code))
            {
                throw new InvalidOperationException("PowerShell authentication code is null or empty.");
            }

            PowerShell ps = context.Items["PS_INSTANCE"] as PowerShell
                  ?? throw new InvalidOperationException("PowerShell instance not found in context items.");
            if (ps.Runspace == null)
            {
                throw new InvalidOperationException("PowerShell runspace is not set. Ensure PowerShellRunspaceMiddleware is registered.");
            }


            ps.AddScript(code, useLocalScope: true)
            .AddParameter("providedKey", providedKey);
            var psResults = await ps.InvokeAsync().ConfigureAwait(false);

            if (psResults.Count == 0 || psResults[0] == null || psResults[0].BaseObject is not bool isValid)
            {
                Log.Error("PowerShell script did not return a valid boolean result.");
                return false;
            }
            Log.Information("Basic authentication result for {ProvidedKey}: {IsValid}", providedKey, isValid);
            return isValid;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error during Basic authentication for {ProvidedKey}", providedKey);
            return false;
        }
    }


    /// <summary>
    /// Builds a PowerShell-based API key validator delegate using the provided authentication code settings.
    /// </summary>
    /// <param name="codeSettings">The settings containing the PowerShell authentication code.</param>
    /// <returns>A delegate that validates an API key using PowerShell.</returns>
    public static Func<HttpContext, string, byte[], Task<bool>> BuildPsValidator(AuthenticationCodeSettings codeSettings)
      => async (ctx, providedKey, providedKeyBytes) =>
      {
          return await ValidatePowerShellKeyAsync(codeSettings.Code, ctx, providedKey);
      };

    /// <summary>
    /// Builds a C#-based API key validator delegate using the provided authentication code settings.
    /// </summary>
    /// <param name="codeSettings">The settings containing the C# authentication code.</param>
    /// <returns>A delegate that validates an API key using C# code.</returns>
    public static Func<HttpContext, string, byte[], Task<bool>> BuildCsValidator(AuthenticationCodeSettings codeSettings)
    {
        var script = CSharpDelegateBuilder.Compile(codeSettings.Code, Serilog.Log.ForContext<ApiKeyAuthHandler>(),
            codeSettings.ExtraImports, codeSettings.ExtraRefs,
        new Dictionary<string, object?>
            {
                { "providedKey", string.Empty },
                { "providedKeyBytes", Array.Empty<byte>() }
            }, languageVersion: codeSettings.CSharpVersion);

        return async (ctx, providedKey, providedKeyBytes) =>
        {
            if (Log.IsEnabled(Serilog.Events.LogEventLevel.Debug))
                Log.Debug("Running C# authentication script for user: {ProvidedKey}", providedKey);
            var krRequest = await KestrunRequest.NewRequest(ctx);
            var krResponse = new KestrunResponse(krRequest);
            var context = new KestrunContext(krRequest, krResponse, ctx);
            var globals = new CsGlobals(SharedStateStore.Snapshot(), context, new Dictionary<string, object?>
            {
                { "providedKey", providedKey },
                { "providedKeyBytes", providedKeyBytes },
            });
            var result = await script.RunAsync(globals).ConfigureAwait(false);
            Log.Information("C# authentication result for {ProvidedKey}: {Result}", providedKey, result.ReturnValue);
            return result.ReturnValue is true;
        };
    }

    /// <summary>
    /// Builds a VB.NET-based API key validator delegate using the provided authentication code settings.
    /// </summary>
    /// <param name="codeSettings">The settings containing the VB.NET authentication code.</param>
    /// <returns>A delegate that validates an API key using VB.NET code.</returns>
    public static Func<HttpContext, string, byte[], Task<bool>> BuildVBNetValidator(AuthenticationCodeSettings codeSettings)
    {
        var script = VBNetDelegateBuilder.Compile<bool>(codeSettings.Code, Serilog.Log.ForContext<BasicAuthHandler>(),
        codeSettings.ExtraImports, codeSettings.ExtraRefs,
        new Dictionary<string, object?>
            {
                { "providedKey", string.Empty },
                { "providedKeyBytes", Array.Empty<byte>() }
            }, languageVersion: codeSettings.VisualBasicVersion);

        return async (ctx, providedKey, providedKeyBytes) =>
        {
            if (Log.IsEnabled(Serilog.Events.LogEventLevel.Debug))
                Log.Debug("Running VB.NET authentication script for user: {ProvidedKey}", providedKey);
            var krRequest = await KestrunRequest.NewRequest(ctx);
            var krResponse = new KestrunResponse(krRequest);
            var context = new KestrunContext(krRequest, krResponse, ctx);
            var globals = new CsGlobals(SharedStateStore.Snapshot(), context, new Dictionary<string, object?>
            {
                { "providedKey", providedKey },
                { "providedKeyBytes", providedKeyBytes },
            });
            // Run the VB.NET script and get the result
            // Note: The script should return a boolean indicating success or failure
            var result = await script(globals).ConfigureAwait(false);

            Log.Information("VB.NET authentication result for {ProvidedKey}: {Result}", providedKey, result);
            if (result is bool isValid)
            {
                return isValid;
            }
            return false;
        };
    }

}