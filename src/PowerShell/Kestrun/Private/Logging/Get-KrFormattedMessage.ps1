function Get-KrFormattedMessage{
	param(
		[Parameter(Mandatory = $true)]
		[Serilog.ILogger]$Logger,

		[Parameter(Mandatory = $true)]
		[Serilog.Events.LogEventLevel]$LogLevel,

		[parameter(Mandatory = $true)]
		[AllowEmptyString()]
		[string]$MessageTemplate,

		[Parameter(Mandatory = $false)]
		[AllowNull()]
		[object[]]$PropertyValues,

		[Parameter(Mandatory = $false)]
		[AllowNull()]
		[System.Exception]$Exception
	)

	$parsedTemplate = $null
	$boundProperties = $null
	if ($Logger.BindMessageTemplate($MessageTemplate, $PropertyValues, [ref]$parsedTemplate, [ref]$boundProperties))
	{
		$logEvent = [Serilog.Events.LogEvent]::new([System.DateTimeOffset]::Now, $LogLevel, $Exception, $parsedTemplate, $boundProperties)
		$strWriter = [System.IO.StringWriter]::new()
		# Use the global TextFormatter if available, otherwise use the default formatter from Kestrun.Logging
		[Kestrun.Logging]::TextFormatter.Format($logEvent, $strWriter)
		$message = $strWriter.ToString()
		$strWriter.Dispose()
		$message
	}
}