function Add-KrClaimPolicy {
    <#
       .SYNOPSIS
           Adds a claim policy to the specified claim policy builder.
       .DESCRIPTION
           This function adds a new claim policy to the provided claim policy builder instance.
       .OUTPUTS
           [Kestrun.Claims.ClaimPolicyBuilder]
           The updated claim policy builder instance.
       .EXAMPLE
           $builder = New-KrClaimPolicy| Add-KrClaimPolicy -Builder $builder -PolicyName "AdminOnly" -ClaimType "role" -AllowedValues "admin"|Build-KrClaimPolicy
           This example adds a new claim policy to the builder.
       .NOTES
           .This function is part of the Kestrun.Claims module and is used to manage claim policies.
           .Maps to ClaimPolicyBuilder.AddPolicy method.
    #>
    [KestrunRuntimeApi('Everywhere')]
    [CmdletBinding(DefaultParameterSetName = 'ClaimType')]
    [OutputType([Kestrun.Claims.ClaimPolicyBuilder])]
    param(
        [Parameter(Mandatory = $true, ValueFromPipeline)]
        [Kestrun.Claims.ClaimPolicyBuilder] $Builder,
        [Parameter(Mandatory = $true)]
        [string] $PolicyName,

        [Parameter(Mandatory = $true, ParameterSetName = 'ClaimType')]
        [string] $ClaimType,
        [Parameter(Mandatory = $true, ParameterSetName = 'UserClaimType')]
        [Kestrun.Claims.UserIdentityClaim] $UserClaimType,
        [Parameter(Mandatory = $true)]
        [string[]] $AllowedValues
    )
    begin {
        if ($UserClaimType) {
            $ClaimType = [Kestrun.Claims.KestrunClaimExtensions]::ToClaimUri($UserClaimType)
        }
    }
    process {
        return $Builder.AddPolicy($PolicyName, $ClaimType, $AllowedValues)
    }
}