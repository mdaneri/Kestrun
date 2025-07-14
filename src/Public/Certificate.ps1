
# Short alias to save typing
$Kcm = [KestrunLib.CertificateManager]

# ---------------------------------------------------------------------------
function New-KestrunSelfSignedCertificate {
<#
.SYNOPSIS
    Generates a self-signed X.509 certificate (RSA or ECDSA).

.EXAMPLE
    $cert = New-KestrunSelfSignedCertificate -DnsName localhost,127.0.0.1 `
                -KeyType Rsa -KeyLength 2048 -ValidDays 30 -Exportable
#>
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]
        [string[]]  $DnsName,

        [ValidateSet('Rsa','Ecdsa')]
        [string]    $KeyType      = 'Rsa',

        [ValidateRange(256,8192)]
        [int]       $KeyLength    = 2048,

        [ValidateRange(1,3650)]
        [int]       $ValidDays    = 365,

        [switch]    $Ephemeral,
        [switch]    $Exportable
    )

    $opts = [KestrunLib.CertificateManager+SelfSignedOptions]::new(
        $DnsName,
        [KestrunLib.CertificateManager+KeyType]::$KeyType,
        $KeyLength,
        $null,      # purposes
        $ValidDays,
        $Ephemeral.IsPresent,
        $Exportable.IsPresent
    )

    return $Kcm::NewSelfSigned($opts)
}

# ---------------------------------------------------------------------------
function New-KestrunCertificateRequest {
<#
.SYNOPSIS
    Creates a PEM-encoded CSR (and returns the private key).

.EXAMPLE
    $csr, $priv = New-KestrunCertificateRequest -DnsName 'example.com' -Country US
#>
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]
        [string[]] $DnsName,

        [ValidateSet('Rsa','Ecdsa')]
        [string]   $KeyType      = 'Rsa',

        [int]      $KeyLength    = 2048,

        [string]   $Country,
        [string]   $Org,
        [string]   $OrgUnit,
        [string]   $CommonName
    )

    $opts = [KestrunLib.CertificateManager+CsrOptions]::new(
        $DnsName,
        [KestrunLib.CertificateManager+KeyType]::$KeyType,
        $KeyLength,
        $Country,
        $Org,
        $OrgUnit,
        $CommonName
    )

    return $Kcm::NewCertificateRequest($opts)
}

# ---------------------------------------------------------------------------
function Import-KestrunCertificate {
<#
.SYNOPSIS
    Imports a PFX/PEM certificate file and returns X509Certificate2.
#>
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)][string] $Path,
        [string] $Password
    )

    return $Kcm::Import($Path, $Password)
}

# ---------------------------------------------------------------------------
function Export-KestrunCertificate {
<#
.SYNOPSIS
    Exports an X509Certificate2 to PFX or PEM(+key).

.EXAMPLE
    Export-KestrunCertificate -Certificate $cert -FilePath 'C:\certs\my' `
            -Format Pem -Password 'p@ss' -IncludePrivateKey
#>
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)][System.Security.Cryptography.X509Certificates.X509Certificate2] $Certificate,
        [Parameter(Mandatory)][string]   $FilePath,

        [ValidateSet('Pfx','Pem')]
        [string] $Format = 'Pfx',

        [string] $Password,
        [switch] $IncludePrivateKey
    )

    $fmtEnum = [KestrunLib.CertificateManager+ExportFormat]::$Format
    $Kcm::Export($Certificate, $FilePath, $fmtEnum, $Password,
                 $IncludePrivateKey.IsPresent)
}

# ---------------------------------------------------------------------------
function Test-KestrunCertificate {
<#
.SYNOPSIS
    Validates a certificate’s chain, EKU, and cryptographic strength.

.EXAMPLE
    Test-KestrunCertificate -Certificate $cert -DenySelfSigned -CheckRevocation
#>
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]
        [System.Security.Cryptography.X509Certificates.X509Certificate2] $Certificate,

        [switch] $CheckRevocation,
        [switch] $AllowWeakAlgorithms,
        [switch] $DenySelfSigned,

        [string[]] $ExpectedPurpose,
        [switch]   $StrictPurpose
    )

    $oidColl = if ($ExpectedPurpose) {
        $oc = [System.Security.Cryptography.OidCollection]::new()
        foreach ($p in $ExpectedPurpose) { $oc.Add([System.Security.Cryptography.Oid]::new($p)) }
        $oc
    } else { $null }

    return $Kcm::Validate($Certificate,
                          $CheckRevocation.IsPresent,
                          $AllowWeakAlgorithms.IsPresent,
                          $DenySelfSigned.IsPresent,
                          $oidColl,
                          $StrictPurpose.IsPresent)
}

# ---------------------------------------------------------------------------
function Get-KestrunCertificatePurpose {
<#
.SYNOPSIS
    Lists the Enhanced Key Usage values on a certificate.
#>
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]
        [System.Security.Cryptography.X509Certificates.X509Certificate2] $Certificate
    )

    return $Kcm::GetPurposes($Certificate)
}

Export-ModuleMember -Function *-Kestrun*
