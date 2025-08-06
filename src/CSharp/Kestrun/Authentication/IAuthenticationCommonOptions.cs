using System.Security.Claims;

namespace Kestrun.Authentication;

 
/// <summary>
/// Defines common options for authentication, including code validation, claim issuance, and claim policy configuration.
/// </summary>
public interface IAuthenticationCommonOptions
{

  /// <summary>
  /// Settings for the authentication code, if using a script.
  /// </summary>
  /// <remarks>
  /// This allows you to specify the language, code, and additional imports/refs.
  /// </remarks>
  public AuthenticationCodeSettings ValidateCodeSettings { get; set; }

  /// <summary>
  /// After credentials are valid, this is called to add extra Claims.
  /// Parameters: HttpContext, username → IEnumerable of extra claims.
  /// </summary>
  public Func<HttpContext, string, Task<IEnumerable<Claim>>>? IssueClaims { get; set; }
  /// <summary>
  /// After credentials are valid, this is called to add extra Claims synchronously.
  /// Parameters: HttpContext, username → IEnumerable of extra claims.
  /// </summary>
  public Func<HttpContext, string, IEnumerable<Claim>>? NativeIssueClaims { get; set; }
  /// <summary>
  /// Settings for the claims issuing code, if using a script.
  /// </summary>
  /// <remarks>
  /// This allows you to specify the language, code, and additional imports/refs for claims issuance.
  /// </remarks>
  public AuthenticationCodeSettings IssueClaimsCodeSettings { get; set; }

  /// <summary>
  /// Gets or sets the claim policy configuration.
  /// </summary>
  /// <remarks>
  /// This allows you to define multiple authorization policies based on claims.
  /// Each policy can specify a claim type and allowed values.
  /// </remarks>
  public ClaimPolicyConfig? ClaimPolicyConfig { get; set; }
}