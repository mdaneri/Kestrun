
function Limit-KrJWTValidity {
    <#
    .SYNOPSIS
        Sets the validity period for the JWT token.
    .DESCRIPTION
        This function sets the validity period for the JWT token, specifying how long the token will be valid.
    .PARAMETER Builder
        The JWT token builder to modify.
    .PARAMETER Lifetime
        The duration for which the JWT token will be valid.
    .OUTPUTS
        [Kestrun.Security.JwtTokenBuilder]
        The modified JWT token builder.
    .EXAMPLE
        $builder = New-KrJWTTokenBuilder | Limit-KrJWTValidity -Lifetime (New-TimeSpan -Hours 1)
        This example creates a new JWT token builder and sets its validity period to 1 hour.
    .NOTES
        This function is part of the Kestrun.Security module and is used to build JWT tokens
        Maps to JwtTokenBuilder.ValidFor
    .LINK
        https://docs.microsoft.com/en-us/dotnet/api/system.identitymodel.tokens.jwt.jwtsecuritytoken?view=azure-dotnet
    #>
    [CmdletBinding()]
    [OutputType([Kestrun.Security.JwtTokenBuilder])]
    param(
        [Parameter(Mandatory = $true, ValueFromPipeline)]
        [Kestrun.Security.JwtTokenBuilder] $Builder,
        [Parameter(Mandatory)]
        [TimeSpan] $Lifetime
    )
    process { 
        return $Builder.ValidFor($Lifetime) 
    }
}
