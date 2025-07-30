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

public class ApiKeyAuthHandler
    : AuthenticationHandler<ApiKeyAuthenticationOptions>
{
    public ApiKeyAuthHandler(
        IOptionsMonitor<ApiKeyAuthenticationOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder)
        : base(options, logger, encoder) { }

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
            var username = Options.ResolveUsername?.Invoke(providedKey) ?? "ApiKeyClient";

            var claims = new List<Claim> { new(ClaimTypes.Name, username) };

            // If the consumer wired up IssueClaims, invoke it now:
            if (Options.IssueClaims is not null)
            {
                var extra = Options.IssueClaims(Context, username);
                if (extra is not null)
                    claims.AddRange(extra);
            }
            var identity = new ClaimsIdentity(claims, Scheme.Name);
            var principal = new ClaimsPrincipal(identity);
            var ticket = new AuthenticationTicket(principal, Scheme.Name);
            Options.Logger.Information("API Key authentication succeeded for identity: {Username}", username);

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


    public static Func<HttpContext, string, byte[], Task<bool>> BuildPsValidator(AuthenticationCodeSettings codeSettings)
      => async (ctx, providedKey, providedKeyBytes) =>
      {
          return await ValidatePowerShellKeyAsync(codeSettings.Code, ctx, providedKey);
      };

    public static Func<HttpContext, string, byte[], Task<bool>> BuildCsValidator(AuthenticationCodeSettings codeSettings)
    {
        var script = CSharpDelegateBuilder.Compile(codeSettings.Code, Serilog.Log.ForContext<ApiKeyAuthHandler>(),
            codeSettings.ExtraImports, codeSettings.ExtraRefs,
        new Dictionary<string, object?>
            {
                { "providedKey", string.Empty },
                { "providedKeyBytes", Array.Empty<byte>() }
            });

        return async (ctx, providedKey, providedKeyBytes) =>
        {
            var krRequest = await KestrunRequest.NewRequest(ctx);
            var krResponse = new KestrunResponse(krRequest);
            var context = new KestrunContext(krRequest, krResponse, ctx);
            var globals = new CsGlobals(SharedStateStore.Snapshot(), context, new Dictionary<string, object?>
            {
                { "providedKey", providedKey },
                { "providedKeyBytes", providedKeyBytes },
            });
            var result = await script.RunAsync(globals).ConfigureAwait(false);
            return result.ReturnValue is true;
        };
    }
}