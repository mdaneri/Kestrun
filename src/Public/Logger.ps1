<#
.SYNOPSIS
  Expose Serilog from PowerShell via Write-KrLog functions.

.DESCRIPTION
  Defines:
    • Write-KrLog   – generic with –Level: Verbose|Debug|Information|Warning|Error|Fatal
    • Write-KrLogVerbose, Write-KrLogDebug, etc. shortcuts
#>

function Write-KrLog {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)][ValidateSet('Verbose', 'Debug', 'Information', 'Warning', 'Error', 'Fatal')]
        [string]$Level,

        [Parameter(Mandatory)][string]$Message,

        [Parameter()][Exception]$Exception
    )

    switch ($Level) {
        'Verbose' { if ($Exception) { [Serilog.Log]::Verbose($Exception, $Message) } else { [Serilog.Log]::Verbose($Message) } }
        'Debug' { if ($Exception) { [Serilog.Log]::Debug($Exception, $Message) } else { [Serilog.Log]::Debug($Message) } }
        'Information' { if ($Exception) { [Serilog.Log]::Information($Exception, $Message) } else { [Serilog.Log]::Information($Message) } }
        'Warning' { if ($Exception) { [Serilog.Log]::Warning($Exception, $Message) } else { [Serilog.Log]::Warning($Message) } }
        'Error' { if ($Exception) { [Serilog.Log]::Error($Exception, $Message) } else { [Serilog.Log]::Error($Message) } }
        'Fatal' { if ($Exception) { [Serilog.Log]::Fatal($Exception, $Message) } else { [Serilog.Log]::Fatal($Message) } }
    }
}

 