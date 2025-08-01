# JwtBuilder.psm1

#—— Load the assembly if needed ——
# Add-Type -Path "path\to\Kestrun.Security.dll"

     
 
 

# maps to JwtTokenBuilder.AddHeader :contentReference[oaicite:22]{index=22}
 
function Protect-KrJWTPayload {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true, ValueFromPipeline)]
        [Kestrun.Security.JwtTokenBuilder] $Builder,
        [Parameter(Mandatory)]
        [byte[]] $KeyBytes,
        [string] $KeyAlg = 'dir',
        [string] $EncAlg = 'A256CBC-HS512'
    )
    process { $Builder.EncryptWithSecret($KeyBytes, $KeyAlg, $EncAlg) }
} # maps to JwtTokenBuilder.EncryptWithSecret :contentReference[oaicite:26]{index=26}

 