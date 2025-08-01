using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using Microsoft.IdentityModel.Tokens;

namespace Kestrun.Security;

/// <summary>
/// Represents all parameters extracted from a JWT, including header fields, standard properties, and claims.
/// </summary>
public class JwtParameters
{
    // Header fields
    /// <summary>
    /// Gets the JWT header fields as a dictionary.
    /// </summary>
    public IDictionary<string, object> Header { get; init; } = new Dictionary<string, object>();

    // Standard properties
    /// <summary>
    /// Gets the issuer ("iss") claim from the JWT.
    /// </summary>
    public string? Issuer     { get; init; }
    /// <summary>
    /// Gets the audiences ("aud") claim from the JWT.
    /// </summary>
    public IEnumerable<string> Audiences { get; init; } = Array.Empty<string>();
    /// <summary>
    /// Gets the subject ("sub") claim from the JWT.
    /// </summary>
    public string? Subject    { get; init; }
    /// <summary>
    /// Gets the "nbf" (Not Before) claim from the JWT, indicating the time before which the token is not valid.
    /// </summary>
    public DateTime? NotBefore{ get; init; }
    /// <summary>
    /// Gets the "exp" (Expiration Time) claim from the JWT, indicating the time after which the token expires.
    /// </summary>
    public DateTime? Expires  { get; init; }
    /// <summary>
    /// Gets the "iat" (Issued At) claim from the JWT, indicating when the token was issued.
    /// </summary>
    public DateTime? IssuedAt { get; init; }
    /// <summary>
    /// Gets the algorithm ("alg") used to sign the JWT.
    /// </summary>
    public string? Algorithm  { get; init; }
    /// <summary>
    /// Gets the key ID ("kid") from the JWT header.
    /// </summary>
    public string? KeyId      { get; init; }
    /// <summary>
    /// Gets the type ("typ") of the JWT, indicating the token type.
    /// </summary>
    public string? Type       { get; init; }

    // All payload claims
    /// <summary>
    /// Gets all claims from the JWT payload, including custom claims, as a dictionary.
    /// </summary>
    public IDictionary<string, object> Claims { get; init; } = new Dictionary<string, object>();
}

/// <summary>
/// Provides methods for inspecting and extracting parameters from JWT tokens.
/// </summary>
public static class JwtInspector
{
    /// <summary>
    /// Reads out every header field, standard property, and claim from a compact JWT.
    /// </summary>
    public static JwtParameters ReadAllParameters(string token)
    {
        var handler = new JwtSecurityTokenHandler();

        // parse without validating signature or lifetime
        var jwt = handler.ReadJwtToken(token);

        var result = new JwtParameters
        {
            Issuer      = jwt.Issuer,
            Audiences   = jwt.Audiences,
            Subject     = jwt.Subject,
            NotBefore   = jwt.ValidFrom == DateTime.MinValue ? null : jwt.ValidFrom,
            Expires     = jwt.ValidTo   == DateTime.MinValue ? null : jwt.ValidTo,
            IssuedAt    = jwt.Payload.IssuedAt == DateTime.MinValue ? null : jwt.Payload.IssuedAt,
            Algorithm   = jwt.SignatureAlgorithm,
            Type        = jwt.Header.Typ,
            KeyId       = jwt.Header.Kid
        };

        // copy all header entries
        foreach (var kv in jwt.Header)
        {
            result.Header[kv.Key] = kv.Value!;
        }

        // copy all payload claims (including custom ones)
        foreach (var claim in jwt.Claims)
        {
            // if a claim type can appear multiple times, you might want to handle lists
            result.Claims[claim.Type] = claim.Value;
        }

        return result;
    }
}
