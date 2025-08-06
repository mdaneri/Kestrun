using Microsoft.AspNetCore.Authorization;

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
 