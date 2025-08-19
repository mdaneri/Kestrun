
namespace Kestrun.Certificates;

using Org.BouncyCastle.Crypto;


/// <summary>
/// Represents the result of creating a Certificate Signing Request (CSR), including the PEM-encoded CSR and the private key.
/// </summary>
/// <param name="Pem">The PEM-encoded CSR string.</param>
/// <param name="PrivateKey">The private key associated with the CSR.</param>
public record CsrResult(string Pem, AsymmetricKeyParameter PrivateKey);