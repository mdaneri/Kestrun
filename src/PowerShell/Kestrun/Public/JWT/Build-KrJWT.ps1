function Build-KrJWT {
    <#
    .SYNOPSIS
        Builds the JWT token from the builder.
    .DESCRIPTION
        This function finalizes the JWT token construction by invoking the Build method on the JwtTokenBuilder instance.
    .PARAMETER Builder
        The JWT token builder to finalize.
    .OUTPUTS
        [Kestrun.Security.JwtBuilderResult]
        The constructed JWT token.
    .EXAMPLE
        $token = New-KrJWTTokenBuilder | Add-KrJWTSubject -Subject "mySubject" |
                  Add-KrJWTIssuer -Issuer "myIssuer" | 
                  Add-KrJWTAudience -Audience "myAudience" | 
                  Build-KrJWT
        This example creates a new JWT token builder, adds a subject, issuer, and audience, and then builds the JWT token.
    .NOTES
        This function is part of the Kestrun.Security module and is used to build JWT tokens.
        Maps to JwtTokenBuilder.Build
    .LINK
        https://docs.microsoft.com/en-us/dotnet/api/system.identitymodel.tokens.jwt.jwtsecuritytoken?view=azure-dotnet
    #>
    [CmdletBinding()]
    [OutputType([Kestrun.Security.JwtBuilderResult])]
    param(
        [Parameter(Mandatory = $true, ValueFromPipeline)]
        [Kestrun.Security.JwtTokenBuilder] $Builder
    )
    process {
        return $Builder.Build()
    }
}
