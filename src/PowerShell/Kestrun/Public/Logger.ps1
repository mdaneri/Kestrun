function New-KrLogger {
  <#
    .SYNOPSIS
    Begin configuration of a named Kestrun logger.

    .PARAMETER Name
    Unique name for the logger (e.g. "api", "auth", "ps").

    .PARAMETER Level
    Minimum log level. Defaults to Information.

    .OUTPUTS
    Returns a Kestrun.KestrunLoggerBuilder object, suitable for piping.
    #>
  [CmdletBinding(SupportsShouldProcess = $true)]
  [OutputType([Kestrun.KestrunLoggerBuilder])]
  param(
    [Parameter(Mandatory = $true)]
    [string]$Name,

    [Parameter()]
    [ValidateSet('Verbose', 'Debug', 'Information', 'Warning', 'Error', 'Fatal')]
    [string]$Level = 'Information'
  )

  $enumLevel = [Serilog.Events.LogEventLevel]::$Level
  if ($PSCmdlet.ShouldProcess("Logger '$Name'", "Create new logger with minimum level $Level")) {
    $builder = [Kestrun.KestrunLogConfigurator]::Configure($Name)
    $builder.Minimum($enumLevel) | Out-Null
    return $builder
  }
}

function Add-KrProperty {
  <#
    .SYNOPSIS
    Adds a static property to every event in the named logger.

    .PARAMETER Builder
    The KestrunLoggerBuilder returned from New-KrLogger.

    .PARAMETER Name
    Name of the property.

    .PARAMETER Value
    Value of the property.

    .OUTPUTS
    Returns the same builder for further chaining.
    #>
  [CmdletBinding()]
  [OutputType([Kestrun.KestrunLoggerBuilder])]
  param(
    [Parameter(Mandatory, ValueFromPipeline)]
    [Kestrun.KestrunLoggerBuilder]$Builder,

    [Parameter(Mandatory = $true)]
    [string]$Name,

    [Parameter(Mandatory = $true)]
    $Value
  )
  process {
    $Builder.WithProperty($Name, $Value) | Out-Null
    return $Builder
  }
}

function Add-KrEnricher {
  <#
    .SYNOPSIS
    Adds an ILogEventEnricher to the logger.

    .PARAMETER Builder
    The KestrunLoggerBuilder returned from New-KrLogger.

    .PARAMETER TypeName
    Full type name of the enricher (e.g. 'Serilog.Enrichers.Thread.ThreadIdEnricher').

    .PARAMETER Args
    Optional constructor arguments for the enricher type.

    .OUTPUTS
    Returns the same builder for further chaining.
    #>
  [CmdletBinding()]
  [OutputType([Kestrun.KestrunLoggerBuilder])]
  param(
    [Parameter(Mandatory, ValueFromPipeline)]
    [Kestrun.KestrunLoggerBuilder]$Builder,

    [Parameter(Mandatory = $true)]
    [string]$TypeName,

    [Parameter()]
    [object[]]$Arguments = @()
  )
  process {
    # Resolve the type
    $type = [Type]::GetType($TypeName)
    if (-not $type) {
      throw "Enricher type '$TypeName' not found."
    }
    # Invoke the builder helper; it picks the correct overload
    if ($Arguments.Count -eq 0) {
      $Builder.With([Type]$type) | Out-Null
    }
    else {
      $Builder.With([Type]$type, $Arguments) | Out-Null
    }
    return $Builder
  }
}

function Add-KrSink {
  <#
    .SYNOPSIS
    Adds a sink to the logger.

    .PARAMETER Builder
    The KestrunLoggerBuilder returned from New-KrLogger.

    .PARAMETER Type
    The sink type: Console, File, Seq, Http, Syslog, or Custom.

    .PARAMETER Options
    A hashtable of options specific to the sink type.

    .PARAMETER CustomSink
    If Type = 'Custom', pass an instantiated object that implements ILogEventSink.

    .OUTPUTS
    Returns the same builder for further chaining.
    #>
  [CmdletBinding()]
  [OutputType([Kestrun.KestrunLoggerBuilder])]
  param(
    [Parameter(Mandatory, ValueFromPipeline)]
    [Kestrun.KestrunLoggerBuilder]$Builder,

    [Parameter(Mandatory = $true)]
    [ValidateSet('Console', 'File', 'Seq', 'Http', 'Syslog', 'Custom')]
    [string]$Type,

    [Parameter()]
    [hashtable]$Options = @{}, 

    [Parameter()]
    $CustomSink
  )
  process {
    switch ($Type) {
      'Console' {
        $template = $Options['OutputTemplate']
        if ($template) {
          $Builder.Sink({ param($w) $w.Console($template) }) | Out-Null
        }
        else {
          $Builder.Sink({ param($w) $w.Console() }) | Out-Null
        }
      }
      'File' {
        if (-not $Options.ContainsKey('Path')) {
          throw "File sink requires -Options @{ Path = '...' }"
        }
        $path = $Options['Path']
        $interval = $Options['RollingInterval']  # e.g. 'Day'
        $formatter = $Options['Formatter']        # e.g. JsonFormatter instance
        if ($formatter) {
          $Builder.Sink({ param($w) $w.File($path, $interval, $formatter) }) | Out-Null
        }
        else {
          $Builder.Sink({ param($w) $w.File($path, $interval) }) | Out-Null
        }
      }
      'Seq' {
        if (-not $Options['ServerUrl']) {
          throw "Seq sink requires -Options @{ ServerUrl = 'http://...' }"
        }
        $Builder.Sink({ param($w) $w.Seq($Options['ServerUrl']) }) | Out-Null
      }
      'Http' {
        if (-not $Options['RequestUri']) {
          throw "Http sink requires -Options @{ RequestUri = 'https://...' }"
        }
        $Builder.Sink({ param($w) $w.DurableHttpUsingFileSizeRolledBuffers($Options['RequestUri']) }) | Out-Null
      }
      'Syslog' {
        if (-not ($Options['Host'] -and $Options['Port'])) {
          throw "Syslog sink requires -Options @{ Host='host'; Port=514 }"
        }
        $Builder.Sink({ param($w) $w.UdpSyslog($Options['Host'], $Options['Port']) }) | Out-Null
      }
      'Custom' {
        if (-not $CustomSink) {
          throw "Custom sink requires -CustomSink parameter with an ILogEventSink instance"
        }
        $Builder.Sink({ param($w) $w.Sink($CustomSink) }) | Out-Null
      }
    }
    return $Builder
  }
}

function Register-KrLogger {
  <#
.SYNOPSIS
  Registers the configured logger and optionally makes it the global default.
.DESCRIPTION
  Finalizes the KestrunLoggerBuilder returned from New-KrLogger (and any Add-* calls),
  creating the actual Serilog logger, registering it under its name, and
  (if -Default is specified) replacing Serilog.Log.Logger.
.PARAMETER Builder
  The KestrunLoggerBuilder object from the pipeline.
.PARAMETER Default
  If present, this logger becomes the new global Serilog.Log.Logger.
.EXAMPLE
  # Register “api” without replacing the global default
  New-KrLogger -Name "api" -Level Debug |
    Add-KrProperty -Name "Subsystem" -Value "API" |
    Add-KrSink -Type File -Options @{ Path = "logs/api-.log"; RollingInterval = 'Day' } |
    Register-KrLogger
.EXAMPLE
  # Register “default” and promote it to global default
  New-KrLogger -Name "default" -Level Information |
    Add-KrSink -Type Console |
    Register-KrLogger -Default
#>
  [CmdletBinding()]
  [OutputType([Kestrun.KestrunLoggerBuilder])]
  param(
    [Parameter(Mandatory, ValueFromPipeline)]
    [Kestrun.KestrunLoggerBuilder]$Builder,

    [Parameter()]
    [switch]$Default
  )
  process {
    # Call into the builder’s Apply(setAsDefault) under the hood
    $Builder.Apply($Default.IsPresent) | Out-Null
  }
}


function Write-KrLog {
  <#
    .SYNOPSIS
    Writes a log event to a named logger, with optional exception and structured properties.

    .PARAMETER Name
    (Optional) Logger name previously configured via New-KrLogger.
    If omitted, the static Serilog.Log is used.

    .PARAMETER Level
    Log level (Verbose, Debug, Information, Warning, Error, Fatal).

    .PARAMETER Message
    Message template.

    .PARAMETER Arguments
    Positional arguments for the template.

    .PARAMETER Exception
    [Optional] An Exception object to attach; if provided, uses the ILogger overload that accepts an exception.

    .PARAMETER Properties
    [Optional] Hashtable of additional structured properties to include on the event.

    .PARAMETER Object
    [Optional] A PowerShell object (PSCustomObject, hashtable, etc.) to log.  Will be JSON-serialized into a property named "Data".
  #>
  [CmdletBinding()]
  param(
    [Parameter(Position = 0)]
    [string]$Name,

    [Parameter(Mandatory = $true)]
    [ValidateSet('Verbose', 'Debug', 'Information', 'Warning', 'Error', 'Fatal')]
    [string]$Level,

    [Parameter(Mandatory = $true)]
    [string]$Message,

    [Parameter()]
    [object[]]$Arguments = @(),

    [Parameter()]
    [System.Exception]$Exception,

    [Parameter()]
    [hashtable]$Properties,

    [Parameter()]
    $Object
  )

  # 1️⃣  Choose target: named logger or global static
  if ($PSBoundParameters.ContainsKey('Name') -and $Name) {
    $logger = [Kestrun.KestrunLogConfigurator]::Get($Name)
    if (-not $logger) {
      throw "Logger '$Name' not found. Did you call New-KrLogger and Register-KrLogger?"
    }
  }
  else {
    $logger = [Serilog.Log]
  }

  # 2️⃣  Push structured properties (if any) into Serilog's LogContext
  $scopes = @()
  if ($Properties) {
    foreach ($k in $Properties.Keys) {
      $scope = [Serilog.Context.LogContext]::PushProperty($k, $Properties[$k])
      $scopes += $scope
    }
  }

  # 3️⃣  If -Object is provided, serialize it to JSON and push as "Data"
  if ($PSBoundParameters.ContainsKey('Object')) {
    $json = $Object | ConvertTo-Json -Depth 5
    $scopes += [Serilog.Context.LogContext]::PushProperty('Data', $json)
  }

  try {
    # 4️⃣  Invoke the correct ILogger overload
    $method = $Level

    if ($PSBoundParameters.ContainsKey('Exception')) {
      # e.g. $logger.Error($exception, $message, $arguments)
      $logger.$method($Exception, $Message, $Arguments)
    }
    else {
      # e.g. $logger.Information($message, $arguments)
      $logger.$method($Message, $Arguments)
    }
  }
  finally {
    # 5️⃣  Dispose scopes in reverse order
    [Array]::Reverse($scopes)
    foreach ($s in $scopes) { $s.Dispose() }
  }
}


function Update-KrLogger {
  <#
      .SYNOPSIS
      Reconfigure an existing logger: add sinks, change level, etc.

      .PARAMETER Name
      Existing logger name.

      .PARAMETER ScriptBlock
      A script block that receives the underlying [Serilog.LoggerConfiguration]
      so you can call any of its methods (.WriteTo.File(), .MinimumLevel.Is(), …).

      .PARAMETER Default
      If provided, the new logger replaces the global Serilog.Log.Logger.

      .EXAMPLE
      # Raise level to Warning and add Console sink
      Update-KestrunLogger -Name "ps" -ScriptBlock {
          param($cfg)
          $cfg.MinimumLevel.Is([Serilog.Events.LogEventLevel]::Warning) |
              Out-Null
          $cfg.WriteTo.Console() | Out-Null
      }
    #>
  [CmdletBinding(SupportsShouldProcess = $true)]
  param(
    [Parameter(Mandatory = $true)]
    [string]$Name,

    [Parameter(Mandatory = $true)]
    [ScriptBlock]$ScriptBlock,

    [switch]$Default
  )
  if (-not [Kestrun.KestrunLogConfigurator]::Exists($Name)) {
    throw "Logger '$Name' not found. Did you call New-KrLogger and Register-KrLogger?"
  }
  # Ensure the script block is a delegate that takes LoggerConfiguration
  if (-not $ScriptBlock -or [string]::IsNullOrEmpty($ScriptBlock.ToString())) {
    throw "ScriptBlock must be a valid script block that takes a LoggerConfiguration parameter."
  }

  if ($PSCmdlet.ShouldProcess("Logger '$Name'", "Update logger configuration")) {
    # Build a delegate that calls the user’s scriptblock
    $action = [System.Action[Serilog.LoggerConfiguration]] {
      param($cfg)
      & $ScriptBlock $cfg   # invoke SB with the cfg as $_ or $args[0]
    }

    [Kestrun.KestrunLogConfigurator]::Reconfigure($Name, $action, $Default.IsPresent)
  }
}


function Get-KrLogger {
  <#
    .SYNOPSIS
    Retrieves a configured Kestrun logger by name.

    .PARAMETER Name
    The name of the logger to retrieve.

    .OUTPUTS
    Returns the Kestrun.KestrunLoggerBuilder for further configuration or inspection.
  #>
  [CmdletBinding()]
  [OutputType([Kestrun.KestrunLoggerBuilder])]
  param(
    [Parameter(Mandatory = $true)]
    [string]$Name
  )

  $logger = [Kestrun.KestrunLogConfigurator]::Get($Name)
  if (-not $logger) {
    throw "Logger '$Name' not found. Did you call New-KrLogger and Register-KrLogger?"
  }
  return $logger
}


function Test-KrLogger {
  <#
    .SYNOPSIS
    Checks if a Kestrun logger with the specified name exists.

    .PARAMETER Name
    The name of the logger to check.

    .OUTPUTS
    Returns $true if the logger exists, otherwise $false.
  #>
  [CmdletBinding()]
  param(
    [Parameter(Mandatory = $true)]
    [string]$Name
  )

  return [Kestrun.KestrunLogConfigurator]::Exists($Name)
}


function Reset-KrLogger {
  <#
    .SYNOPSIS
    Resets a configured Kestrun logger by name.

    .PARAMETER Name
    The name of the logger to reset.
  #>
  [CmdletBinding(SupportsShouldProcess = $true)]
  param(
    [Parameter(Mandatory = $true)]
    [string]$Name
  )

  if (-not [Kestrun.KestrunLogConfigurator]::Exists($Name)) {
    throw "Logger '$Name' not found. Did you call New-KrLogger and Register-KrLogger?"
  }

  if ($PSCmdlet.ShouldProcess("Logger '$Name'", "Reset logger")) {
    [Kestrun.KestrunLogConfigurator]::Reset($Name)
  }
}

