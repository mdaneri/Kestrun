using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using Microsoft.IdentityModel.Tokens;

namespace Kestrun.Security;

public class JwtParameters
{
    // Header fields
    public IDictionary<string, object> Header { get; init; } = new Dictionary<string, object>();

    // Standard properties
    public string? Issuer     { get; init; }
    public IEnumerable<string> Audiences { get; init; } = Array.Empty<string>();
    public string? Subject    { get; init; }
    public DateTime? NotBefore{ get; init; }
    public DateTime? Expires  { get; init; }
    public DateTime? IssuedAt { get; init; }
    public string? Algorithm  { get; init; }
    public string? Type       { get; init; }
    public string? KeyId      { get; init; }

    // All payload claims
    public IDictionary<string, object> Claims { get; init; } = new Dictionary<string, object>();
}

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
