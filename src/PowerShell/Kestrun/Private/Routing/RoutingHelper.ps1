[Diagnostics.CodeAnalysis.SuppressMessageAttribute('PSUseApprovedVerbs', '')]
[Diagnostics.CodeAnalysis.SuppressMessageAttribute('PSUseSingularNouns', '')]
param()
# In your module scope
$script:KrRouteGroupStack = [System.Collections.Stack]::new()

function _KrJoin-Route([string]$base, [string]$child) {
    $b = ($base ?? '').TrimEnd('/')
    $c = ($child ?? '').TrimStart('/')
    if ([string]::IsNullOrWhiteSpace($b)) { "/$c".TrimEnd('/') -replace '^$', '/' }
    elseif ([string]::IsNullOrWhiteSpace($c)) { if ($b) { $b } else { '/' } }
    else { "$b/$c" }
}

function _KrMerge-Unique([string[]]$a, [string[]]$b) {
    @(($a + $b | Where-Object { $_ -ne $null } | Select-Object -Unique))
}

function _KrMerge-Args([hashtable]$a, [hashtable]$b) {
    if (-not $a) { return $b }
    if (-not $b) { return $a }
    $m = @{}
    foreach ($k in $a.Keys) { $m[$k] = $a[$k] }
    foreach ($k in $b.Keys) { $m[$k] = $b[$k] } # child overrides
    $m
}



# Create a new MapRouteOptions from an existing one + overrides
function _KrWith-MRO {
    param(
        [Parameter(Mandatory)][Kestrun.Hosting.Options.MapRouteOptions]$Base,
        [Parameter()][hashtable]$Override = @{}
    )
    $h = @{
        Pattern          = $Base.Pattern
        HttpVerbs        = $Base.HttpVerbs
        Code             = $Base.Code
        Language         = $Base.Language
        ExtraImports     = $Base.ExtraImports
        ExtraRefs        = $Base.ExtraRefs
        RequireSchemes   = $Base.RequireSchemes
        RequirePolicies  = $Base.RequirePolicies
        CorsPolicyName   = $Base.CorsPolicyName
        Arguments        = $Base.Arguments
        OpenAPI          = $Base.OpenAPI
        ThrowOnDuplicate = $Base.ThrowOnDuplicate
    }
    foreach ($k in $Override.Keys) { $h[$k] = $Override[$k] }
    return New-MapRouteOption -Property $h
}

# Merge two MapRouteOptions (parent first, then child overrides)
function _KrMerge-MRO {
    param(
        [Parameter(Mandatory)][Kestrun.Hosting.Options.MapRouteOptions]$Parent,
        [Parameter(Mandatory)][Kestrun.Hosting.Options.MapRouteOptions]$Child
    )
    $pattern = if ($Child.Pattern) {
        if ($Parent.Pattern) { "$($Parent.Pattern)/$($Child.Pattern)" } else { $Child.Pattern }
    } else { $Parent.Pattern }

    $extraRefs = if ($null -ne $Child.ExtraRefs) { 
        if($Parent.ExtraRefs) {
            $Parent.ExtraRefs + $Child.ExtraRefs
        } else {
            $Child.ExtraRefs
        }
    } else { $Parent.ExtraRefs }

    $merged = @{
        Pattern          = $pattern.Replace('//', '/')
        HttpVerbs        = if ($null -ne $Child.HttpVerbs -and ($Child.HttpVerbs.Count -gt 0)) { $Child.HttpVerbs } else { $Parent.HttpVerbs }
        Code             = if ($Child.Code) { $Child.Code } else { $Parent.Code }
        Language         = if ($null -ne $Child.Language) { $Child.Language } else { $Parent.Language }
        ExtraImports     = _KrMerge-Unique $Parent.ExtraImports   $Child.ExtraImports
        ExtraRefs        = $extraRefs
        RequireSchemes   = _KrMerge-Unique $Parent.RequireSchemes  $Child.RequireSchemes
        RequirePolicies  = _KrMerge-Unique $Parent.RequirePolicies $Child.RequirePolicies
        CorsPolicyName   = if ($Child.CorsPolicyName) { $Child.CorsPolicyName } else { $Parent.CorsPolicyName }
        Arguments        = _KrMerge-Args   $Parent.Arguments       $Child.Arguments
        OpenAPI          = if ($Child.OpenAPI) { $Child.OpenAPI } else { $Parent.OpenAPI }
        ThrowOnDuplicate = $Child.ThrowOnDuplicate -or $Parent.ThrowOnDuplicate
    }
    return New-MapRouteOption -Property $merged
}
