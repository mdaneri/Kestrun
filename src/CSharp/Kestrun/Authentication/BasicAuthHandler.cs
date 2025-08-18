using System.Collections;
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
using Serilog.Core;

namespace Kestrun.Authentication;

/// <summary>
/// Handles Basic Authentication for HTTP requests.
/// </summary>
public class BasicAuthHandler : AuthenticationHandler<BasicAuthenticationOptions>, IAuthHandler
{
    // private Serilog.ILogger Logger => Options.Logger;

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
        : base(options, loggerFactory, encoder)
    {
        if (options.CurrentValue.Logger.IsEnabled(Serilog.Events.LogEventLevel.Debug))
            options.CurrentValue.Logger.Debug("BasicAuthHandler initialized");
    }

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
            if (Options.ValidateCredentialsAsync is null)
                return Fail("No credentials validation function provided");

            if (Options.RequireHttps && !Request.IsHttps)
                return Fail("HTTPS required");

            if (!Request.Headers.TryGetValue(Options.HeaderName, out var authHeaderVal))
                return Fail("Missing Authorization Header");

            var authHeader = AuthenticationHeaderValue.Parse(authHeaderVal.ToString());

            var schemeValidation = ValidateSchemeAndParameter(authHeader);
            if (schemeValidation is not null)
                return schemeValidation;

            Log.Information("Processing Basic Authentication for header: {Context}", Context);

            var decode = TryDecodeCredentials(authHeader.Parameter!, Options.Base64Encoded);
            if (!decode.Success)
                return Fail(decode.Error!);

            var parse = TryParseCredentials(decode.Value!);
            if (!parse.Success)
                return Fail(parse.Error!);

            var user = parse.Username!;
            var pass = parse.Password!;
            var valid = await Options.ValidateCredentialsAsync(Context, user, pass);
            if (!valid)
                return Fail("Invalid credentials");

            Options.Logger.Information("Basic auth succeeded for user: {User}", user);

            var ticket = await IAuthHandler.GetAuthenticationTicketAsync(Context, user, Options, Scheme);
            Options.Logger.Information("Basic auth ticket created for user: {User}", user);
            return AuthenticateResult.Success(ticket);
        }
        catch (Exception ex)
        {
            // Log the exception and return a failure result
            Options.Logger.Error(ex, "Error processing Authentication");
            return Fail("Exception during authentication");
        }
    }

    private AuthenticateResult? ValidateSchemeAndParameter(AuthenticationHeaderValue authHeader)
    {
        if (Options.Base64Encoded && !string.Equals(authHeader.Scheme, "Basic", StringComparison.OrdinalIgnoreCase))
            return Fail("Invalid Authorization Scheme");

        if (string.IsNullOrEmpty(authHeader.Parameter))
            return Fail("Missing credentials in Authorization Header");

        if ((authHeader.Parameter?.Length ?? 0) > 8 * 1024)
            return Fail("Header too large");

        return null;
    }

    private (bool Success, string? Value, string? Error) TryDecodeCredentials(string parameter, bool base64)
    {
        try
        {
            var raw = base64
                ? Encoding.UTF8.GetString(Convert.FromBase64String(parameter ?? string.Empty))
                : parameter ?? string.Empty;
            return (true, raw, null);
        }
        catch (FormatException)
        {
            Options.Logger.Warning("Invalid Base64 in Authorization header");
            return (false, null, "Malformed credentials");
        }
    }

    private (bool Success, string? Username, string? Password, string? Error) TryParseCredentials(string rawCreds)
    {
        var match = Options.SeparatorRegex.Match(rawCreds);
        if (!match.Success || match.Groups.Count < 3)
            return (false, null, null, "Malformed credentials");

        var user = match.Groups[1].Value;
        var pass = match.Groups[2].Value;
        if (string.IsNullOrEmpty(user))
            return (false, null, null, "Username cannot be empty");

        return (true, user, pass, null);
    }

    private AuthenticateResult Fail(string reason)
    {
        Options.Logger.Warning("Basic auth failed: {Reason}", reason);
        return AuthenticateResult.Fail(reason);
    }

    /// <summary>
    /// Handles the challenge response for Basic Authentication.
    /// </summary>
    /// <param name="properties">The authentication properties.</param>
    /// <remarks>
    /// This method is called to challenge the client for credentials if authentication fails.
    /// If the request is not secure, it does not challenge with WWW-Authenticate.     
    /// If the SuppressWwwAuthenticate option is set, it does not add the WWW-Authenticate header.   
    /// If the Realm is set, it includes it in the WWW-Authenticate header. 
    /// If the request is secure, it adds the WWW-Authenticate header with the Basic scheme.    
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
    /// Builds a PowerShell-based validator function for authenticating users.
    /// </summary>
    /// <param name="settings">The authentication code settings containing the PowerShell script.</param>
    /// <param name="logger">The logger instance.</param>
    /// <returns>A function that validates credentials using the provided PowerShell script.</returns>
    /// <remarks>
    /// This method compiles the PowerShell script and returns a delegate that can be used to validate user credentials.
    /// </remarks>
    public static Func<HttpContext, string, string, Task<bool>> BuildPsValidator(AuthenticationCodeSettings settings, Serilog.ILogger logger)
    {
        if (logger.IsEnabled(Serilog.Events.LogEventLevel.Debug))
            logger.Debug("BuildPsValidator  settings: {Settings}", settings);

        return async (ctx, user, pass) =>
        {
            return await IAuthHandler.ValidatePowerShellAsync(settings.Code, ctx, new Dictionary<string, string>
            {
                { "username", user },
                { "password", pass }
            },logger);
        };
    }
    /// <summary>
    /// Builds a C#-based validator function for authenticating users.
    /// </summary>
    /// <param name="settings">The authentication code settings containing the C# script.</param>
    /// <param name="logger">The logger instance.</param>
    /// <returns>A function that validates credentials using the provided C# script.</returns>
    public static Func<HttpContext, string, string, Task<bool>> BuildCsValidator(AuthenticationCodeSettings settings, Serilog.ILogger logger)
    {
        if (logger.IsEnabled(Serilog.Events.LogEventLevel.Debug))
            logger.Debug("BuildCsValidator  settings: {Settings}", settings);

        // pass the settings to the core C# validator
        var core = IAuthHandler.BuildCsValidator(
            settings,
            logger.ForContext<BasicAuthHandler>(),
            ("username", string.Empty), ("password", string.Empty)
            ) ?? throw new InvalidOperationException("Failed to build C# validator delegate from provided settings.");
        return (ctx, username, password) =>
            core(ctx, new Dictionary<string, object?>
            {
                ["username"] = username,
                ["password"] = password
            });
    }

    /// <summary>
    /// Builds a VB.NET-based validator function for authenticating users.
    /// </summary>
    /// <param name="settings">The authentication code settings containing the VB.NET script.</param>
    /// <param name="logger">The logger instance.</param>
    /// <returns>A function that validates credentials using the provided VB.NET script.</returns>
    public static Func<HttpContext, string, string, Task<bool>> BuildVBNetValidator(AuthenticationCodeSettings settings, Serilog.ILogger logger)
    {
        if (logger.IsEnabled(Serilog.Events.LogEventLevel.Debug))
            logger.Debug("BuildCsValidator  settings: {Settings}", settings);
        // pass the settings to the core VB.NET validator
        var core = IAuthHandler.BuildVBNetValidator(
            settings,
            logger.ForContext<BasicAuthHandler>(),
            ("username", string.Empty), ("password", string.Empty)) ?? throw new InvalidOperationException("Failed to build VB.NET validator delegate from provided settings.");
        return (ctx, username, password) =>
            core(ctx, new Dictionary<string, object?>
            {
                ["username"] = username,
                ["password"] = password
            });
    }
}