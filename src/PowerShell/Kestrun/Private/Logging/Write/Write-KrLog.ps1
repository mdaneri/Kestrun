function Write-KrLog {
	[Cmdletbinding()]
	param(
		[Parameter(Mandatory = $true)]
		[Serilog.Events.LogEventLevel]$LogLevel,

		[Parameter(Mandatory = $true)]
		[AllowEmptyString()]
		[AllowNull()]
		[string]$MessageTemplate,

		[Parameter(Mandatory = $false)]
		[string]$Name,

		[Parameter(Mandatory = $false)]
		[AllowNull()]
		[System.Exception]$Exception,

		[Parameter(Mandatory = $false)]
		[AllowNull()]
		[System.Management.Automation.ErrorRecord]$ErrorRecord,

		[Parameter(Mandatory = $false)]
		[AllowNull()]
		[object[]]$PropertyValues,

		[Parameter(Mandatory = $false)]
		[switch]$PassThru
	)
	try {
		# If ErrorRecord is available wrap it into RuntimeException
		if ($null -ne $ErrorRecord) {

			if ($null -eq $Exception) {
				$Exception = $ErrorRecord.Exception
			}

			$Exception = [Kestrun.Logging.Exceptions.WrapperException]::new($Exception, $ErrorRecord)
		}
		if ([string]::IsNullOrEmpty($Name)) {
			$Logger = [Kestrun.Logging.LoggerManager]::DefaultLogger
		}
		else {
			$Logger = [Kestrun.Logging.LoggerManager]::Get($Name)
		}
		if ($null -eq $Logger) {
			throw "Logger with name '$Name' not found. Please ensure it is registered before logging."
		}
		switch ($LogLevel) {
			Verbose {
				$Logger.Verbose($Exception, $MessageTemplate, $PropertyValues)
			}
			Debug { 
				$Logger.Debug($Exception, $MessageTemplate, $PropertyValues)
			}
			Information { 
				$Logger.Information($Exception, $MessageTemplate, $PropertyValues)
			}
			Warning { 
				$Logger.Warning($Exception, $MessageTemplate, $PropertyValues)
			}
			Error { 
				$Logger.Error($Exception, $MessageTemplate, $PropertyValues)
			}
			Fatal { 
				$Logger.Fatal($Exception, $MessageTemplate, $PropertyValues)
			}
		}

		if ($PassThru) {
			Get-KrFormattedMessage -Logger $Logger -LogLevel $LogLevel -MessageTemplate $MessageTemplate -PropertyValues $PropertyValues -Exception $Exception
		}
	}
	catch {
		# If an error occurs while logging, write to the default logger
		$defaultLogger = [Kestrun.Logging.LoggerManager]::DefaultLogger
		if ($null -ne $defaultLogger) {
			$defaultLogger.Error($_, "Error while logging message: {MessageTemplate}", $MessageTemplate)
		}
		else {
			Write-Error "Error while logging message: $_"
		}
		throw $_
	}
}