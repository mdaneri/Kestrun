namespace Kestrun.Claims;


/// <summary>Represents one “claim must equal …” rule.</summary>
/// <remarks>
/// This is used to define authorization policies that require a specific claim type
/// with specific allowed values.
/// It is typically used in conjunction with <see cref="ClaimPolicyConfig"/> to define
/// multiple policies.
/// </remarks>
public sealed record ClaimRule(string ClaimType, params string[] AllowedValues);