
function Copy-KrJWTTokenBuilder {
    <#
    .SYNOPSIS
       Creates a new cloned JWT token builder instance.
    .DESCRIPTION
       This function creates a new cloned instance of the JwtTokenBuilder class, which is used to construct JWT tokens.
    .EXAMPLE
       # Creates a new cloned JWT token builder instance
       $builder = $oldBuilder|New-KrJWTToken

    .EXAMPLE
       # Creates a new cloned JWT token builder instance
       $builder = New-KrJWTToken -Builder $oldBuilder

       $builder.WithSubject('admin')
               .WithIssuer('https://issuer')
               .WithAudience('api')
               .SignWithSecret('uZ6zDP3CGK3rktmVOXQk8A')   # base64url
               .EncryptWithCertificate($cert,'RSA-OAEP','A256GCM')
               .Build()

    .OUTPUTS
        [Kestrun.Security.JwtTokenBuilder]
        A new cloned instance of the JwtTokenBuilder class.
    .NOTES
        This function is part of the Kestrun.Security module and is used to build JWT tokens.
        Maps to JwtTokenBuilder.New
    #>
    [KestrunRuntimeApi([KestrunApiContext]::Everywhere)]
    [CmdletBinding(SupportsShouldProcess = $true)]
    [OutputType([Kestrun.Security.JwtTokenBuilder])]
    param(   [Parameter(Mandatory = $true, ValueFromPipeline)]
        [Kestrun.Security.JwtTokenBuilder] $Builder)
    process {
        # Create a new JWT token builder instance
        if ($PSCmdlet.ShouldProcess("JwtTokenBuilder", "Create new JWT token builder")) {
            return $Builder.CloneBuilder()
        }
    }
}
