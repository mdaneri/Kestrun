# JwtBuilder.psm1

#—— Load the assembly if needed ——
# Add-Type -Path "path\to\Kestrun.Security.dll"

function New-KrJwtTokenBuilder {
    [CmdletBinding()]
    param()
    [Kestrun.Security.JwtTokenBuilder]::New()
}

function Set-KrJwtIssuer {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory, ValueFromPipeline)]
        [Kestrun.Security.JwtTokenBuilder] $Builder,
        [Parameter(Mandatory)]
        [string] $Issuer
    )
    process { $Builder.WithIssuer($Issuer) }
} # maps to JwtTokenBuilder.WithIssuer :contentReference[oaicite:16]{index=16}

function Set-KrJwtAudience {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory, ValueFromPipeline)]
        [Kestrun.Security.JwtTokenBuilder] $Builder,
        [Parameter(Mandatory)]
        [string] $Audience
    )
    process { $Builder.WithAudience($Audience) }
} # maps to JwtTokenBuilder.WithAudience :contentReference[oaicite:17]{index=17}

function Set-KrJwtSubject {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory, ValueFromPipeline)]
        [Kestrun.Security.JwtTokenBuilder] $Builder,
        [Parameter(Mandatory)]
        [string] $Subject
    )
    process { $Builder.WithSubject($Subject) }
} # maps to JwtTokenBuilder.WithSubject :contentReference[oaicite:18]{index=18}

function Add-KrJwtClaim {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory, ValueFromPipeline)]
        [Kestrun.Security.JwtTokenBuilder] $Builder,
        [Parameter(Mandatory)]
        [string] $Type,
        [Parameter(Mandatory)]
        [string] $Value
    )
    process { $Builder.AddClaim($Type, $Value) }
} # maps to JwtTokenBuilder.AddClaim :contentReference[oaicite:19]{index=19}

function Set-KrJwtValidFor {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory, ValueFromPipeline)]
        [Kestrun.Security.JwtTokenBuilder] $Builder,
        [Parameter(Mandatory)]
        [TimeSpan] $Lifetime
    )
    process { $Builder.ValidFor($Lifetime) }
} # maps to JwtTokenBuilder.ValidFor :contentReference[oaicite:20]{index=20}

function Set-KrJwtNotBefore {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory, ValueFromPipeline)]
        [Kestrun.Security.JwtTokenBuilder] $Builder,
        [Parameter(Mandatory)]
        [DateTime] $UtcBefore
    )
    process { $Builder.NotBefore($UtcBefore) }
} # maps to JwtTokenBuilder.NotBefore :contentReference[oaicite:21]{index=21}

function Add-KrJwtHeader {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory, ValueFromPipeline)]
        [Kestrun.Security.JwtTokenBuilder] $Builder,
        [Parameter(Mandatory)]
        [string] $Name,
        [Parameter(Mandatory)]
        [object] $Value
    )
    process { $Builder.AddHeader($Name, $Value) }
} # maps to JwtTokenBuilder.AddHeader :contentReference[oaicite:22]{index=22}

function Sign-JwtWithSecret {
    [CmdletBinding(DefaultParameterSetName = 'Base64Url')]
    param(
        [Parameter(Mandatory, ValueFromPipeline)]
        [Kestrun.Security.JwtTokenBuilder] $Builder,
        [Parameter(Mandatory = $true, ParameterSetName = 'Base64Url')]
        [string] $Base64Url,
        [Parameter(Mandatory = $true, ParameterSetName = 'HexadecimalKey')]
        [string] $HexadecimalKey,  # optional, if provided, will be used instead of Base64Url
        [Parameter(Mandatory = $true, ParameterSetName = 'Passphrase')]
        [string] $Passphrase,
        [Parameter()]
        [Kestrun.Security.JwtAlgorithm] $Algorithm = 'Auto'
    )
    process {
        $algEnum = [Kestrun.Security.JwtAlgorithm]::$Algorithm
        switch ($PSCmdlet.ParameterSetName) {
            'Base64Url' {
                $Builder.SignWithSecret($Base64Url, $algEnum)
            }
            'HexadecimalKey' {
                $Builder.SignWithSecretHex($HexadecimalKey, $algEnum) 
            }
            'Passphrase' {
                if ([string]::IsNullOrEmpty($Passphrase)) {
                    throw [System.ArgumentException]::new("Passphrase cannot be null or empty.", 'Passphrase')
                }
                $Builder.SignWithSecretPassphrase($Passphrase, $algEnum) 
            }
            Default {
                throw [System.ArgumentException]::new("Invalid parameter set name: $($xPSCmdlet.ParameterSetName).", 'ParameterSetName')
            }
        }
    }
} # maps to JwtTokenBuilder.SignWithSecret :contentReference[oaicite:23]{index=23}

function Set-KrJwtWithRsaPem {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory, ValueFromPipeline)]
        [Kestrun.Security.JwtTokenBuilder] $Builder,
        [Parameter(Mandatory)]
        [string] $PemPath,
        [ValidateSet('Auto', 'RS256', 'RS384', 'RS512', 'PS256', 'PS384', 'PS512', 'ES256', 'ES384', 'ES512')]
        [string] $Algorithm = 'Auto'
    )
    process {
        $algEnum = [Kestrun.Security.JwtAlgorithm]::$Algorithm
        $Builder.SignWithRsaPem($PemPath, $algEnum)
    }
} # maps to JwtTokenBuilder.SignWithRsaPem :contentReference[oaicite:24]{index=24}

function Set-KrJwtWithCertificate {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory, ValueFromPipeline)]
        [Kestrun.Security.JwtTokenBuilder] $Builder,
        [Parameter(Mandatory)]
        [System.Security.Cryptography.X509Certificates.X509Certificate2] $Cert,
        [ValidateSet('Auto', 'RS256', 'RS384', 'RS512', 'PS256', 'PS384', 'PS512', 'ES256', 'ES384', 'ES512')]
        [string] $Algorithm = 'Auto'
    )
    process {
        $algEnum = [Kestrun.Security.JwtAlgorithm]::$Algorithm
        $Builder.SignWithCertificate($Cert, $algEnum)
    }
} # maps to JwtTokenBuilder.SignWithCertificate :contentReference[oaicite:25]{index=25}

function Protect-KrJwtWithSecret {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory, ValueFromPipeline)]
        [Kestrun.Security.JwtTokenBuilder] $Builder,
        [Parameter(Mandatory)]
        [byte[]] $KeyBytes,
        [string] $KeyAlg = 'dir',
        [string] $EncAlg = 'A256CBC-HS512'
    )
    process { $Builder.EncryptWithSecret($KeyBytes, $KeyAlg, $EncAlg) }
} # maps to JwtTokenBuilder.EncryptWithSecret :contentReference[oaicite:26]{index=26}

function Build-KrJwt {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory, ValueFromPipeline)]
        [Kestrun.Security.JwtTokenBuilder] $Builder
    )
    process { $Builder.Build() }
} # maps to JwtTokenBuilder.Build :contentReference[oaicite:27]{index=27}

function Get-KrJwtToken {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory, ValueFromPipeline)]
        [Kestrun.Security.JwtBuilderResult] $Result
    )
    process { $Result.Token() }
} # maps to JwtBuilderResult.Token :contentReference[oaicite:28]{index=28}

function Get-KrJwtValidation {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory, ValueFromPipeline)]
        [Kestrun.Security.JwtBuilderResult] $Result,
        [TimeSpan] $ClockSkew = ([TimeSpan]::FromMinutes(1))
    )
    process { $Result.ValidationParameters($ClockSkew) }
} # maps to JwtBuilderResult.ValidationParameters :contentReference[oaicite:29]{index=29}

function Update-KrJwt {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory, ValueFromPipeline)]
        [Kestrun.Security.JwtBuilderResult] $Result,
        [TimeSpan] $Lifetime
    )
    process { $Result.Renew($Lifetime) }
} # maps to JwtBuilderResult.Renew :contentReference[oaicite:30]{index=30}

function Get-KrJwtInfo {
    [CmdletBinding()]
    [OutputType([Kestrun.Security.JwtParameters])]
    param(
        [Parameter(Mandatory)]
        [string] $Token
    )
    process { [Kestrun.Security.JwtInspector]::ReadAllParameters($Token) }
} # maps to JwtInspector.ReadAllParameters :contentReference[oaicite:31]{index=31}
