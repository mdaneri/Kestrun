function Get-KrDefaultLogger {
	<#
	.SYNOPSIS
		Gets the logger for the current session.
	.DESCRIPTION
		Gets the specified logger as the current logger for the session.

	.OUTPUTS	
		Instance of Serilog.ILogger that is currently set as the default logger.
	.EXAMPLE
		PS> $logger = Get-KrDefaultLogger
		Retrieves the current default logger instance for the session.
	.EXAMPLE
		PS> $logger = Get-KrDefaultLogger | Write-Host
		Retrieves the current default logger instance and outputs it to the console.
	.NOTES
		This function is part of the Kestrun logging framework and is used to retrieve the current default logger instance for the session.
		It can be used in scripts and modules that utilize Kestrun for logging.
	#>
	[CmdletBinding()]
	[OutputType([Serilog.ILogger])]
	param( )
	return [Kestrun.Logging.LoggerManager]::DefaultLogger
}