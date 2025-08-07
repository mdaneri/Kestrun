using System.Security.Claims;

namespace Kestrun.Authentication;

 
/// <summary>
/// Defines common options for authentication, including code validation, claim issuance, and claim policy configuration.
/// </summary>
public interface IAuthenticationCommonOptions: IClaimsCommonOptions
{

  /// <summary>
  /// Settings for the authentication code, if using a script.
  /// </summary>
  /// <remarks>
  /// This allows you to specify the language, code, and additional imports/refs.
  /// </remarks>
  public AuthenticationCodeSettings ValidateCodeSettings { get; set; }
 
}