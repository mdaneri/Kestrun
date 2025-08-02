using System.Management.Automation;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text;
using System.Text.Encodings.Web;
using Kestrun.Hosting;
using Kestrun.Languages;
using Kestrun.Models;
using Kestrun.SharedState;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;
using Serilog;

namespace Kestrun.Authentication;

/// <summary>
/// Handles Basic Authentication for HTTP requests.
/// </summary>
public class BasicAuthHandler : AuthenticationHandler<BasicAuthenticationOptions>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="BasicAuthHandler"/> class.
    /// </summary>
    /// <param name="options">The options for Basic Authentication.</param>
    /// <param name="loggerFactory">The logger factory.</param>
    /// <param name="encoder">The URL encoder.</param>
    /// <remarks>
    /// This constructor is used to set up the Basic Authentication handler with the provided options, logger factory, and URL encoder.
    /// </remarks>
    public BasicAuthHandler(
        IOptionsMonitor<BasicAuthenticationOptions> options,
        ILoggerFactory loggerFactory,
        UrlEncoder encoder)
        : base(options, loggerFactory, encoder) { }


    /// <summary>
    /// Handles the authentication process for Basic Authentication.
    /// </summary>
    /// <returns>A task representing the authentication result.</returns>
    /// <remarks>
    /// This method is called to authenticate a user based on the Basic Authentication scheme.
    /// </remarks>
    /// <exception cref="FormatException">Thrown if the Authorization header is not properly formatted.</exception>
    /// <exception cref="ArgumentNullException">Thrown if the Authorization header is null or empty.</exception>
    /// <exception cref="Exception">Thrown for any other unexpected errors during authentication.</exception>
    /// <remarks>
    /// The method checks for the presence of the Authorization header, decodes it, and validates the credentials.
    /// </remarks>
    /// <remarks>
    /// If the credentials are valid, it creates a ClaimsPrincipal and returns a successful authentication result.
    /// If the credentials are invalid, it returns a failure result.
    /// </remarks>
    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        try
        {
            if (Options.ValidateCredentials is null)
                return Fail("No credentials validation function provided");

            // Check if the request is secure (HTTPS) if required
            if (Options.RequireHttps && !Request.IsHttps)
                return Fail("HTTPS required");

            // Check if the Authorization header is present
            if (!Request.Headers.TryGetValue(Options.HeaderName, out var authHeaderVal))
                return Fail("Missing Authorization Header");

            var authHeader = AuthenticationHeaderValue.Parse(authHeaderVal.ToString());
            if (Options.Base64Encoded && !string.Equals(authHeader.Scheme, "Basic", StringComparison.OrdinalIgnoreCase))
                return Fail("Invalid Authorization Scheme");
            // Check if the header is empty
            if (string.IsNullOrEmpty(authHeader.Parameter))
                return Fail("Missing credentials in Authorization Header");
            Log.Information("Processing Basic Authentication for header: {Context}", Context);
            // Check if the header is too large
            if ((authHeader.Parameter?.Length ?? 0) > 8 * 1024) return Fail("Header too large");
            // Decode the credentials
            string rawCreds;
            try
            {
                // Decode the Base64 encoded credentials if required
                rawCreds = Options.Base64Encoded
                  ? Encoding.UTF8.GetString(Convert.FromBase64String(authHeader.Parameter ?? ""))
                  : authHeader.Parameter ?? string.Empty;
            }
            catch (FormatException)
            {
                // Log the error and return a failure result
                Options.Logger.Warning("Invalid Base64 in Authorization header");
                return Fail("Malformed credentials");
            }
            // Use the regex match to extract exactly two groups:
            var match = Options.SeparatorRegex.Match(rawCreds);
            if (!match.Success || match.Groups.Count < 3)
                return Fail("Malformed credentials");

            // Group[1] is username, Group[2] is password:
            var user = match.Groups[1].Value;
            var pass = match.Groups[2].Value;
            // Check if username or password is empty
            if (string.IsNullOrEmpty(user))
                return Fail("Username cannot be empty");
            var valid = await Options.ValidateCredentials(Context, user, pass);
            if (!valid)
                return Fail("Invalid credentials");

            // If credentials are valid, create claims
            Options.Logger.Information("Basic auth succeeded for user: {User}", user);
            var claims = new List<Claim> { new(ClaimTypes.Name, user) };

            // If the consumer wired up IssueClaims, invoke it now:
            if (Options.IssueClaims is not null)
            {
                // Call the IssueClaims function to get additional claims
                var extra = Options.IssueClaims(Context, user);
                if (extra is not null)
                    claims.AddRange(extra);
            }
            // Create the ClaimsIdentity and ClaimsPrincipal
            var identity = new ClaimsIdentity(claims, Scheme.Name);
            // Create the AuthenticationTicket with the principal and scheme name
            var principal = new ClaimsPrincipal(identity);
            // Create the authentication ticket
            var ticket = new AuthenticationTicket(principal, Scheme.Name);
            Options.Logger.Information("Basic auth ticket created for user: {User}", user);
            // Return a successful authentication result
            return AuthenticateResult.Success(ticket);
        }
        catch (Exception ex)
        {
            // Log the exception and return a failure result
            Options.Logger.Error(ex, "Error processing Authentication");
            return Fail("Exception during authentication");
        }

        AuthenticateResult Fail(string reason)
        {
            // Log the failure reason
            Options.Logger.Warning("Basic auth failed: {Reason}", reason);
            // Return a failure result with the reason
            return AuthenticateResult.Fail(reason);
        }
    }

    /// <summary>
    /// Handles the challenge response for Basic Authentication.
    /// </summary>
    /// <param name="properties">The authentication properties.</param>
    /// <remarks>
    /// This method is called to challenge the client for credentials if authentication fails.
    /// </remarks>
    /// <remarks>
    /// If the request is not secure, it does not challenge with WWW-Authenticate.
    /// </remarks>
    /// <remarks>
    /// If the SuppressWwwAuthenticate option is set, it does not add the WWW-Authenticate header.
    /// </remarks>
    /// <remarks>
    /// If the Realm is set, it includes it in the WWW-Authenticate header.
    /// </remarks>
    /// <remarks>
    /// If the request is secure, it adds the WWW-Authenticate header with the Basic scheme.
    /// </remarks>
    /// <remarks>
    /// The response status code is set to 401 Unauthorized.
    /// </remarks>
    /// <returns>A task representing the asynchronous operation.</returns>
    /// <exception cref="InvalidOperationException">Thrown if the Realm is not set and SuppressWwwAuthenticate is false.</exception>    
    protected override Task HandleChallengeAsync(AuthenticationProperties properties)
    {
        if (!Options.SuppressWwwAuthenticate)
        {
            var realm = Options.Realm ?? "Kestrun";
            Response.Headers.WWWAuthenticate = $"Basic realm=\"{realm}\", charset=\"UTF-8\"";
        }
        // If the request is not secure, we don't challenge with WWW-Authenticate
        Response.StatusCode = StatusCodes.Status401Unauthorized;

        return Task.CompletedTask;
    }

    /// <summary>
    /// Handles the forbidden response for Basic Authentication.    
    /// </summary>
    /// <param name="properties">The authentication properties.</param>
    /// <remarks>
    /// This method is called to handle forbidden responses for Basic Authentication.
    /// </remarks>
    protected override Task HandleForbiddenAsync(AuthenticationProperties properties)
    {
        Response.StatusCode = StatusCodes.Status403Forbidden;
        return Task.CompletedTask;
    }

    /// <summary>
    /// Authenticates the user using PowerShell script.
    /// This method is used to validate the username and password against a PowerShell script.
    /// </summary>
    /// <param name="code">The PowerShell script code used for authentication.</param>
    /// <param name="context">The HTTP context.</param>
    /// <param name="username">The username to validate.</param>
    /// <param name="password">The password to validate.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    /// <exception cref="InvalidOperationException"></exception>
    public static async ValueTask<bool> AuthenticatePowerShellAsync(string? code, HttpContext context, string username, string password)
    {
        try
        {
            if (!context.Items.ContainsKey("PS_INSTANCE"))
            {
                throw new InvalidOperationException("PowerShell runspace not found in context items. Ensure PowerShellRunspaceMiddleware is registered.");
            }

            if (string.IsNullOrWhiteSpace(username))
            {
                Log.Warning("Username  is null or empty.");
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
    }


    /// <summary>
    /// Builds a PowerShell-based validator function for authenticating users.
    /// </summary>
    /// <param name="codeSettings">The authentication code settings containing the PowerShell script.</param>
    /// <returns>A function that validates credentials using the provided PowerShell script.</returns>
    public static Func<HttpContext, string, string, Task<bool>> BuildPsValidator(AuthenticationCodeSettings codeSettings)
      => async (ctx, user, pass) =>
      {
          return await AuthenticatePowerShellAsync(codeSettings.Code, ctx, user, pass);
      };

    /// <summary>
    /// Builds a C#-based validator function for authenticating users.
    /// </summary>
    /// <param name="codeSettings">The authentication code settings containing the C# script.</param>
    /// <returns>A function that validates credentials using the provided C# script.</returns>
    public static Func<HttpContext, string, string, Task<bool>> BuildCsValidator(AuthenticationCodeSettings codeSettings)
    {
        var script = CSharpDelegateBuilder.Compile(codeSettings.Code, Serilog.Log.ForContext<BasicAuthHandler>(),
        codeSettings.ExtraImports, codeSettings.ExtraRefs,
        new Dictionary<string, object?>
            {
                { "username", "" },
                { "password", "" }
            });

        return async (ctx, user, pass) =>
        {
            var krRequest = await KestrunRequest.NewRequest(ctx);
            var krResponse = new KestrunResponse(krRequest);
            var context = new KestrunContext(krRequest, krResponse, ctx);
            var globals = new CsGlobals(SharedStateStore.Snapshot(), context, new Dictionary<string, object?>
            {
                { "username", user },
                { "password", pass }
            });
            var result = await script.RunAsync(globals).ConfigureAwait(false);
            return result.ReturnValue is true;
        };
    }
}