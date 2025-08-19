using System;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Microsoft.IdentityModel.Tokens;

namespace Kestrun.Tests.Jwt;

internal sealed class EcdsaEnablingCryptoProviderFactory : CryptoProviderFactory
{
    public override bool IsSupportedAlgorithm(string algorithm, SecurityKey key)
    {
        if (IsEcdsaAlg(algorithm) && (HasEcdsaKey(key)))
        {
            return true;
        }
        return base.IsSupportedAlgorithm(algorithm, key);
    }

    public override SignatureProvider CreateForSigning(SecurityKey key, string algorithm, bool willCreateSignatures = true)
    {
        if (IsEcdsaAlg(algorithm) && key is X509SecurityKey x509)
        {
            var ecdsa = x509.Certificate.GetECDsaPrivateKey();
            if (ecdsa != null)
            {
                return new AsymmetricSignatureProvider(new ECDsaSecurityKey(ecdsa) { KeyId = x509.KeyId ?? x509.Certificate.Thumbprint }, algorithm, willCreateSignatures);
            }
        }
        return base.CreateForSigning(key, algorithm, willCreateSignatures);
    }

    private static bool IsEcdsaAlg(string alg) => alg == SecurityAlgorithms.EcdsaSha256 || alg == SecurityAlgorithms.EcdsaSha384 || alg == SecurityAlgorithms.EcdsaSha512;

    private static bool HasEcdsaKey(SecurityKey key)
        => key is ECDsaSecurityKey
           || (key is X509SecurityKey x && x.Certificate?.GetECDsaPrivateKey() != null);
}

internal static class TestCryptoInit
{
    [ModuleInitializer]
    public static void Init()
    {
        // Enable ECDSA signing even on hosts where the default factory reports unsupported
        CryptoProviderFactory.Default = new EcdsaEnablingCryptoProviderFactory();
    }
}
