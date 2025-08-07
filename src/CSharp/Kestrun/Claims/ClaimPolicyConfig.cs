using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;
using Kestrun.Authentication; // Add the correct namespace for IClaimsCommonOptions

namespace Kestrun.Claims;


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
/// Copies <see cref="ClaimPolicyConfig"/> into <see cref="AuthorizationOptions"/> after configuration.
/// </summary>
public sealed class ClaimPolicyPostConfigurer
    : IPostConfigureOptions<AuthorizationOptions>
{
    private readonly string _scheme;
    private readonly IOptionsMonitor<IClaimsCommonOptions> _basics;

    /// <summary>
    /// Initializes a new instance of the <see cref="ClaimPolicyPostConfigurer"/> class.
    /// </summary>
    /// <param name="scheme">The authentication scheme name.</param>
    /// <param name="basics">The options monitor for <see cref="BasicAuthenticationOptions"/>.</param>
    public ClaimPolicyPostConfigurer(
        string scheme,
        IOptionsMonitor<IClaimsCommonOptions> basics)
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
