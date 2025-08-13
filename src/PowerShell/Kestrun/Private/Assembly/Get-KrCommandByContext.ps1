# Bit mapping: Definition=1, Route=2, Schedule=4 (composites are sums)
function Get-KrFunctionContextMask {
  param([System.Management.Automation.FunctionInfo]$Function)

  if (-not $Function.ScriptBlock) { return 0 }

  $fnAst = $Function.ScriptBlock.Ast.
  Find({ param($n) $n -is [System.Management.Automation.Language.FunctionDefinitionAst] -and $n.Name -eq $Function.Name }, $true)
  if (-not $fnAst) { return 0 }

  $attrs = @()
  if ($fnAst.Attributes) { $attrs += $fnAst.Attributes }
  if ($fnAst.Body -and $fnAst.Body.ParamBlock -and $fnAst.Body.ParamBlock.Attributes) {
    $attrs += $fnAst.Body.ParamBlock.Attributes
  }

  $kr = $attrs | Where-Object { $_.TypeName.Name -eq 'KestrunRuntimeApi' } | Select-Object -First 1
  if (-not $kr) { return 0 }

  $txt = (($kr.PositionalArguments + $kr.NamedArguments.Expression) | Where-Object { $_ }).Extent.Text
  #|           ForEach-Object { $_.Extent.Text } -join ' '
  $mask = switch ($txt) {
    "'Everywhere'" { 7 }
    "'Runtime'" { 6 }
    "'ScheduleAndDefinition'" { 5 }
    "'Definition'" { 1 }
    "'Route'" { 2 }
    "'Schedule'" { 4 }
  }
 
  return $mask
}

# Helper: convert enum/composite name to bitmask
function _KrNameToMask {
  param([Parameter(Mandatory)][string]$Name)
  switch ($Name) {
    'Definition' { 1 }
    'Route' { 2 }
    'Schedule' { 4 }
    'ScheduleAndDefinition' { 5 }
    'Runtime' { 6 }
    'Everywhere' { 7 }
    default { throw "Unknown context '$Name'." }
  }
}

# -------- Functions + compiled cmdlets --------
function Get-KrCommandsByContext {
  <#
    .SYNOPSIS
      Retrieves Kestrun commands based on their context.
    .DESCRIPTION
      This function retrieves Kestrun commands based on their context, allowing for filtering by command type and context.
      It supports both inclusive and exclusive filtering.
    .PARAMETER AnyOf
      Specifies the contexts that the commands must match at least one of.
    .PARAMETER AllOf
      Specifies the contexts that the commands must match all of.
    .PARAMETER Not
      Specifies the contexts that the commands must not match.
    .PARAMETER Module
      The name of the module to search for commands.
    .PARAMETER IncludeNonExported
      Whether to include non-exported commands in the search.
    .PARAMETER Exact
      Whether to match the contexts exactly or allow for partial matches.
    .PARAMETER Functions
      An array of functions to filter by context. If not provided, all functions in the specified module are considered.
    .OUTPUTS
      [System.Management.Automation.CommandInfo[]]
      An array of command information objects that match the specified context.
  #>
  [CmdletBinding(DefaultParameterSetName = 'AnyOf')]
  param(
    [Parameter(Mandatory, ParameterSetName = 'AnyOf')]
    [ValidateSet('Definition', 'Route', 'Schedule', 'ScheduleAndDefinition', 'Runtime', 'Everywhere')]
    [string[]]$AnyOf,

    [Parameter(Mandatory, ParameterSetName = 'AllOf')]
    [ValidateSet('Definition', 'Route', 'Schedule')]
    [string[]]$AllOf,

    [ValidateSet('Definition', 'Route', 'Schedule', 'ScheduleAndDefinition', 'Runtime', 'Everywhere')]
    [string[]]$Not,

    [string]$Module = 'Kestrun',
    [switch]$IncludeNonExported,
    [switch]$Exact,

    [object[]]$Functions
  )

  $cmds = if ($Functions) {
    $Functions
  }
  else {
    if ($IncludeNonExported) { Get-Command -Module $Module -All } else { Get-Command -Module $Module }
  }

  $target = 0
  if ($PSCmdlet.ParameterSetName -eq 'AnyOf') { foreach ($n in $AnyOf) { $target = $target -bor (_KrNameToMask $n) } }
  else { foreach ($n in $AllOf) { $target = $target -bor (_KrNameToMask $n) } }

  $notMask = 0
  foreach ($n in ($Not | ForEach-Object { $_ })) { $notMask = $notMask -bor (_KrNameToMask $n) }

  $match = if ($PSCmdlet.ParameterSetName -eq 'AnyOf') {
    if ($Exact) { { param($m) $m -eq $target } } else { { param($m) ($m -band $target) -ne 0 } }
  }
  else {
    if ($Exact) { { param($m) $m -eq $target } } else { { param($m) ($m -band $target) -eq $target } }
  }

  foreach ($c in $cmds) {
    $m = 0
    if ($c.CommandType -eq 'Function') {
      $m = Get-KrFunctionContextMask -Function $c
    }
    elseif ($c.CommandType -eq 'Cmdlet' -and $c.ImplementingType) {
      $a = $c.ImplementingType.GetCustomAttributes($true) |
      Where-Object { $_.GetType().Name -eq 'KestrunRuntimeApiAttribute' } |
      Select-Object -First 1
      if ($a) { $m = [int]([KestrunApiContext]$a.Contexts) }
    }

    if ($m -eq 0) { continue }
    if ($notMask -ne 0 -and ($m -band $notMask) -ne 0) { continue }  # exclude forbidden bits
    if (& $match $m) { $c }
  }
}


function Get-KrDocSet {
  <#
  .SYNOPSIS
    Get a set of documentation for Kestrun commands.

  .DESCRIPTION
    This function retrieves a set of documentation for Kestrun commands based on the specified context.

  .PARAMETER Name
    The name of the documentation set to retrieve.

  .PARAMETER Module
    The name of the module to search for commands.

  .PARAMETER IncludeNonExported
    Whether to include non-exported commands in the search.

  .EXAMPLE
      # All Definition-only commands (pure)
    Get-KrDocSet -Name DefinitionOnly
  .EXAMPLE
    # All commands that are *only* Route
    Get-KrDocSet -Name RouteOnly
  .EXAMPLE
    # All Route+Schedule commands (pure composite)
    Get-KrDocSet -Name RouteAndSchedule
  .EXAMPLE
    # Everything usable in runtime (pure composite of Route|Schedule)
    Get-KrDocSet -Name Runtime
  .EXAMPLE
    # All config-only commands (pure Definition, no runtime use)
    Get-KrDocSet -Name ConfigOnly
  #>
  [CmdletBinding()]
  param(
    [Parameter(Mandatory)]
    [ValidateSet(
      'DefinitionOnly',
      'RouteOnly',
      'ScheduleOnly',
      'RouteAndSchedule',
      'ScheduleAndDefinition',
      'Runtime',   # Route|Schedule
      'Everywhere',
      'ConfigOnly' # Definition but not Route/Schedule
    )]
    [string]$Name,

    [string]$Module = 'Kestrun',
    [switch]$IncludeNonExported,
    [object[]]$Functions
  )

  switch ($Name) {
    'DefinitionOnly' {
      Get-KrCommandsByContext -AnyOf Definition -Exact -Module $Module -IncludeNonExported:$IncludeNonExported -Functions $Functions
    }
    'RouteOnly' {
      Get-KrCommandsByContext -AnyOf Route -Exact -Module $Module -IncludeNonExported:$IncludeNonExported -Functions $Functions
    }
    'ScheduleOnly' {
      Get-KrCommandsByContext -AnyOf Schedule -Exact -Module $Module -IncludeNonExported:$IncludeNonExported -Functions $Functions
    }
    'RouteAndSchedule' {
      Get-KrCommandsByContext -AllOf Route, Schedule -Exact -Module $Module -IncludeNonExported:$IncludeNonExported -Functions $Functions
    }
    'ScheduleAndDefinition' {
      Get-KrCommandsByContext -AnyOf ScheduleAndDefinition -Exact -Module $Module -IncludeNonExported:$IncludeNonExported -Functions $Functions
    }
    'Runtime' {
      Get-KrCommandsByContext -AnyOf Runtime -Exact -Module $Module -IncludeNonExported:$IncludeNonExported -Functions $Functions
    }
    'Everywhere' {
      Get-KrCommandsByContext -AnyOf Everywhere -Exact -Module $Module -IncludeNonExported:$IncludeNonExported -Functions $Functions
    }
    'ConfigOnly' {
      # Definition but NOT Route or Schedule
      Get-KrCommandsByContext -AnyOf Definition -Exact -Module $Module -IncludeNonExported:$IncludeNonExported -Functions $Functions
    }
  }
}
