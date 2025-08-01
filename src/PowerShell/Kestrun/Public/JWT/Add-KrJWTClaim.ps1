
function Add-KrJWTClaim {
    <#
    .SYNOPSIS
        Adds a claim to the JWT token builder.
    .DESCRIPTION
        This function adds a claim to the JWT token builder, allowing for the specification of additional data.
    .PARAMETER Builder
        The JWT token builder to modify.
    .PARAMETER Type
        The type of the claim to add.
    .PARAMETER Value
        The value of the claim to add.
    .OUTPUTS
        [Kestrun.Security.JwtTokenBuilder]
        The modified JWT token builder.
    .EXAMPLE
        $builder = New-KrJWTTokenBuilder | Add-KrJWTClaim -Type "role" -Value "admin"
        This example creates a new JWT token builder and adds a claim to it.
    .NOTES
        This function is part of the Kestrun.Security module and is used to build JWT tokens
        Maps to JwtTokenBuilder.AddClaim
    .LINK
        https://docs.microsoft.com/en-us/dotnet/api/system.identitymodel.tokens.jwt.jwtsecuritytoken?view=azure-dotnet
    #>
    [CmdletBinding()]
    [OutputType([Kestrun.Security.JwtTokenBuilder])]
    param(
        [Parameter(Mandatory = $true, ValueFromPipeline)]
        [Kestrun.Security.JwtTokenBuilder] $Builder,
        [Parameter(Mandatory)]
        [string] $Type,
        [Parameter(Mandatory)]
        [string] $Value
    )
    process {
        return $Builder.AddClaim($Type, $Value) 
    }
}