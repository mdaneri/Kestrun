[Diagnostics.CodeAnalysis.SuppressMessageAttribute('PSUseApprovedVerbs', '')]
[Diagnostics.CodeAnalysis.SuppressMessageAttribute('PSUseSingularNouns', '')]
param()
# In your module scope
$script:KrRouteGroupStack = [System.Collections.Generic.Stack[hashtable]]::new()

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
