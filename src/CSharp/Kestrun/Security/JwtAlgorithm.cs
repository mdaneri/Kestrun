using Microsoft.IdentityModel.Tokens;

namespace Kestrun.Security;
public enum JwtAlgorithm
{
    Auto,

    HS256,
    HS384,
    HS512,

    RS256,
    RS384,
    RS512,

    PS256,
    PS384,
    PS512,

    ES256,
    ES384,
    ES512
}

public static class JwtAlgorithmExtensions
{
    public static string ToJwtString(this JwtAlgorithm alg, int keyByteLength = 0)
    {
        // handle the “Auto” case only for HMAC
        if (alg == JwtAlgorithm.Auto)
        {
            return keyByteLength switch
            {
                >= 64 => SecurityAlgorithms.HmacSha512,
                >= 48 => SecurityAlgorithms.HmacSha384,
                _     => SecurityAlgorithms.HmacSha256
            };
        }

        return alg switch
        {
            JwtAlgorithm.HS256 => SecurityAlgorithms.HmacSha256,
            JwtAlgorithm.HS384 => SecurityAlgorithms.HmacSha384,
            JwtAlgorithm.HS512 => SecurityAlgorithms.HmacSha512,

            JwtAlgorithm.RS256 => SecurityAlgorithms.RsaSha256,
            JwtAlgorithm.RS384 => SecurityAlgorithms.RsaSha384,
            JwtAlgorithm.RS512 => SecurityAlgorithms.RsaSha512,

            JwtAlgorithm.PS256 => SecurityAlgorithms.RsaSsaPssSha256,
            JwtAlgorithm.PS384 => SecurityAlgorithms.RsaSsaPssSha384,
            JwtAlgorithm.PS512 => SecurityAlgorithms.RsaSsaPssSha512,

            JwtAlgorithm.ES256 => SecurityAlgorithms.EcdsaSha256,
            JwtAlgorithm.ES384 => SecurityAlgorithms.EcdsaSha384,
            JwtAlgorithm.ES512 => SecurityAlgorithms.EcdsaSha512,

            _ => throw new ArgumentOutOfRangeException(nameof(alg), alg, null)
        };
    }
}