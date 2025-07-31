function New-KrJwtToken {
    [CmdletBinding(DefaultParameterSetName = 'HS256', SupportsShouldProcess = $true)]
    param (
        # ───── Claims ───────────────────────────────
        [hashtable] $Claims,
        [string]    $Subject,

        # ───── Standard fields ─────────────────────
        [string]    $Issuer,
        [string]    $Audience,
        [DateTime]  $NotBefore = [DateTime]::UtcNow,
        [TimeSpan]  $Lifetime = (New-TimeSpan -Hours 1),

        # ───── Signing options (pick ONE set) ──────
        [Parameter(ParameterSetName = 'HS256')]
        [string] $Secret,                       # base64url

        [Parameter(ParameterSetName = 'RSA')]
        [string] $RsaPemPath,                  # private key PEM

        [Parameter(ParameterSetName = 'Cert')]
        [System.Security.Cryptography.X509Certificates.X509Certificate2]
        $SigningCertificate,

        # ───── Encryption (optional) ───────────────
        [string] $EncryptForPemPath,     # recipient public key PEM
        [System.Security.Cryptography.X509Certificates.X509Certificate2]
        $EncryptForCertificate,

        # ───── Extra header fields ────────────────
        [hashtable] $AdditionalHeaders
    )

    # Load assembly once
    if (-not ('Kestrun.Security.JwtGenerator' -as [type])) {
        Add-Type -Path (Join-Path $PSScriptRoot 'Kestrun.dll')
    }

    # Build Claim objects
    $claimObjs = @()
    if ($Subject) {
        $claimObjs += [System.Security.Claims.Claim]::new(
            [System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames]::Sub, $Subject)
    }
    if ($Claims) {
        foreach ($k in $Claims.Keys) {
            $claimObjs += [System.Security.Claims.Claim]::new($k, $Claims[$k])
        }
    }

    # ───────────── SigningCredentials ─────────────
    $signingCreds = $null
    switch ($PSCmdlet.ParameterSetName) {
        'HS256' {
            $bytes = [Convert]::FromBase64String($Secret)
            $key = [Microsoft.IdentityModel.Tokens.SymmetricSecurityKey]::new($bytes)
            $signingCreds = [Microsoft.IdentityModel.Tokens.SigningCredentials]::new(
                $key, [Microsoft.IdentityModel.Tokens.SecurityAlgorithms]::HmacSha256)
        }
        'RSA' {
            $pem = Get-Content -Raw -Path $RsaPemPath
            $rsa = [System.Security.Cryptography.RSA]::Create()
            $rsa.ImportFromPem($pem)
            $key = [Microsoft.IdentityModel.Tokens.RsaSecurityKey]::new($rsa)
            $signingCreds = [Microsoft.IdentityModel.Tokens.SigningCredentials]::new(
                $key, [Microsoft.IdentityModel.Tokens.SecurityAlgorithms]::RsaSha256)
        }
        'Cert' {
            if (-not $SigningCertificate.HasPrivateKey) {
                throw "SigningCertificate must contain a private key."
            }
            $key = [Microsoft.IdentityModel.Tokens.X509SecurityKey]::new($SigningCertificate)
            $alg = if ($key.PublicKey.Key -is [System.Security.Cryptography.ECDsa]) {
                [Microsoft.IdentityModel.Tokens.SecurityAlgorithms]::EcdsaSha256
            }
            else {
                [Microsoft.IdentityModel.Tokens.SecurityAlgorithms]::RsaSha256
            }
            $signingCreds = [Microsoft.IdentityModel.Tokens.SigningCredentials]::new($key, $alg)
        }
    }

    # ───────────── EncryptingCredentials ──────────
    $encryptCreds = $null
    if ($EncryptForPemPath) {
        $pubPem = Get-Content -Raw -Path $EncryptForPemPath
        $rsaPub = [System.Security.Cryptography.RSA]::Create()
        $rsaPub.ImportFromPem($pubPem)
        $ekey = [Microsoft.IdentityModel.Tokens.RsaSecurityKey]::new($rsaPub)
        $encryptCreds = [Microsoft.IdentityModel.Tokens.EncryptingCredentials]::new(
            $ekey,
            [Microsoft.IdentityModel.Tokens.SecurityAlgorithms]::RsaOAEP,
            [Microsoft.IdentityModel.Tokens.SecurityAlgorithms]::Aes128CbcHmacSha256)
    }
    elseif ($EncryptForCertificate) {
        $ekey = [Microsoft.IdentityModel.Tokens.X509SecurityKey]::new($EncryptForCertificate)
        $encryptCreds = [Microsoft.IdentityModel.Tokens.EncryptingCredentials]::new(
            $ekey,
            [Microsoft.IdentityModel.Tokens.SecurityAlgorithms]::RsaOAEP,
            [Microsoft.IdentityModel.Tokens.SecurityAlgorithms]::Aes128CbcHmacSha256)
    }

    # ───────────── Call the C# helper ─────────────
    $expiry = [DateTime]::UtcNow.Add($Lifetime)
    if ($PSCmdlet.ShouldProcess("Generate JWT token for subject '$Subject'")) {
        [Kestrun.Security.JwtGenerator]::GenerateJwt(
            $claimObjs, $Issuer, $Audience, $expiry,
            $signingCreds, $NotBefore, $encryptCreds, $AdditionalHeaders)
    }
}
