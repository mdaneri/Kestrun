function Add-KrJWTIssuer {
    <#
    .SYNOPSIS
        Adds an issuer to the JWT token builder.
    .DESCRIPTION
        This function adds an issuer to the JWT token builder, allowing for the specification of the token's issuer.
    .PARAMETER Builder
        The JWT token builder to modify.
    .PARAMETER Issuer
        The issuer to set for the JWT token.
    .OUTPUTS
        [Kestrun.Security.JwtTokenBuilder]
        The modified JWT token builder.
    .EXAMPLE
        $builder = New-KrJWTTokenBuilder | Add-KrJWTIssuer -Issuer "myIssuer"
        This example creates a new JWT token builder and adds an issuer to it.
    .NOTES
        This function is part of the Kestrun.Security module and is used to build JWT tokens
        Maps to JwtTokenBuilder.WithIssuer
    .LINK
        https://docs.microsoft.com/en-us/dotnet/api/system.identitymodel.tokens.jwt.jwtsecuritytoken?view=azure-dotnet
    #>
    [KestrunRuntimeApi('Everywhere')]
    [CmdletBinding()]
    [OutputType([Kestrun.Security.JwtTokenBuilder])]
    param(
        [Parameter(Mandatory = $true, ValueFromPipeline)]
        [Kestrun.Security.JwtTokenBuilder] $Builder,
        [Parameter(Mandatory)]
        [string] $Issuer
    )
    process {
        return $Builder.WithIssuer($Issuer)
    }
}