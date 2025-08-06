using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;

namespace Kestrun.Authentication;

/// <summary>Represents one “claim must equal …” rule.</summary>
/// <remarks>
/// This is used to define authorization policies that require a specific claim type
/// with specific allowed values.
/// It is typically used in conjunction with <see cref="ClaimPolicyConfig"/> to define
/// multiple policies.
/// </remarks>
public sealed record ClaimRule(string ClaimType, params string[] AllowedValues);

/// <summary>A bag of named policies, each backed by a ClaimRule.</summary>
/// <remarks>
/// This is used to define multiple authorization policies in a structured way.
/// </remarks>
public sealed class ClaimPolicyConfig
{
    /// <summary>
    /// Gets the dictionary of named policies, each backed by a <see cref="ClaimRule"/>.
    /// </summary>
    public Dictionary<string, ClaimRule> Policies { get; init; } = [];
}

/// <summary>
/// Extension methods for converting <see cref="ClaimPolicyConfig"/> to authorization delegates.
/// </summary>
/// <remarks>
/// This allows you to easily convert a <see cref="ClaimPolicyConfig"/> into an action delegate
/// that can be used with <c>services.AddAuthorization(...)</c>.
/// This is useful for defining multiple policies in a clean and maintainable way.
/// </remarks>
public static class ClaimPolicyExtensions
{
    /// <summary>
    /// Turns <see cref="ClaimPolicyConfig"/> into the delegate that
    /// <c>services.AddAuthorization(...)</c> needs.
    /// </summary>
    public static Action<AuthorizationOptions> ToAuthzDelegate(
        this ClaimPolicyConfig cfg)
    => options =>
    {
        foreach (var (name, rule) in cfg.Policies)
        {
            options.AddPolicy(name, p =>
                p.RequireClaim(rule.ClaimType, rule.AllowedValues));
        }
    };
}
 

// ------------------------------------------------------------
// 1.  Helper that copies ClaimPolicyConfig → AuthorizationOptions
// ------------------------------------------------------------

/// <summary>
/// Copies <see cref="ClaimPolicyConfig"/> into <see cref="AuthorizationOptions"/> after configuration.
/// </summary>
public sealed class ClaimPolicyPostConfigurer
    : IPostConfigureOptions<AuthorizationOptions>
{
    private readonly string _scheme;
    private readonly IOptionsMonitor<BasicAuthenticationOptions> _basics;

    /// <summary>
    /// Initializes a new instance of the <see cref="ClaimPolicyPostConfigurer"/> class.
    /// </summary>
    /// <param name="scheme">The authentication scheme name.</param>
    /// <param name="basics">The options monitor for <see cref="BasicAuthenticationOptions"/>.</param>
    public ClaimPolicyPostConfigurer(
        string scheme,
        IOptionsMonitor<BasicAuthenticationOptions> basics)
    {
        _scheme = scheme;
        _basics = basics;
    }

    /// <summary>
    /// Applies the <see cref="ClaimPolicyConfig"/> to the specified <see cref="AuthorizationOptions"/> after configuration.
    /// </summary>
    /// <param name="name">The name of the options instance being configured. May be null.</param>
    /// <param name="authz">The <see cref="AuthorizationOptions"/> instance to configure.</param>
    public void PostConfigure(string? name, AuthorizationOptions authz)
    {
        // Only run once for the default options instance
        var claimCfg = _basics.Get(_scheme).ClaimPolicyConfig;
        claimCfg?.ToAuthzDelegate()?.Invoke(authz);
    }
}
