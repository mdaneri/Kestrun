function Add-KrUserClaim {
    <#
       .SYNOPSIS
              Adds a user claim to the specified claims collection.
         .DESCRIPTION
            This function adds a new claim to the provided claims collection, allowing you to specify the claim type and value.
         .PARAMETER Claims
            The collection of claims to which the new claim will be added.
        .PARAMETER ClaimType
            The type of the claim to be added, such as "role", "email", etc.
        .PARAMETER UserClaimType
            The user-specific type of the claim to be added, such as "Admin", "User", etc.
        .PARAMETER Value
            The value of the claim to be added.
       .OUTPUTS
           [System.Security.Claims.Claim[]]
           The updated claims collection.
       .EXAMPLE
           $claims = Add-KrUserClaim -Claims $claims -ClaimType "role" | Add-KrUserClaim -UserClaimType "Admin" -Value "true"
           This example adds a new user claim to the claims collection.
       .NOTES
           .This function is part of the Kestrun.Claims module and is used to manage user claims.
           .Maps to ClaimCollection.Add method.
    #>
    [KestrunRuntimeApi('Route')]
    [CmdletBinding(DefaultParameterSetName = 'ClaimType')]
    [OutputType([System.Security.Claims.Claim[]])]
    [OutputType([System.Array])]
    param(
        [Parameter(ValueFromPipeline)]
        [System.Security.Claims.Claim[]] $Claims,
        [Parameter(Mandatory = $true, ParameterSetName = 'ClaimType')]
        [string] $ClaimType,
        [Parameter(Mandatory = $true, ParameterSetName = 'UserClaimType')]
        [Kestrun.Claims.UserIdentityClaim] $UserClaimType,
        [Parameter(Mandatory = $true)]
        [string] $Value
    )

    begin { $bag = [System.Collections.Generic.List[System.Security.Claims.Claim]]::new() }

    process { if ($null -ne $Claims) { $bag.AddRange($Claims) } }

    end {
        # resolve ClaimType if the user chose the enum parameter-set
        if ($UserClaimType) {
            $ClaimType = [Kestrun.Claims.KestrunClaimExtensions]::ToClaimUri($UserClaimType)
        }

        $bag.Add([System.Security.Claims.Claim]::new($ClaimType, $Value))

        # OUTPUT: one strongly-typed array, not enumerated
        , ([System.Security.Claims.Claim[]] $bag.ToArray())
    }
}