function Set-KrDefaultLogger {
	<#
	.SYNOPSIS
		Sets the logger for the current session.
	.DESCRIPTION
		Sets the specified logger as the current logger for the session.
	.PARAMETER  Name
		The name of the logger to set as the default logger.
	.INPUTS
		Instance of Serilog.ILogger
	.OUTPUTS
		None. This cmdlet does not return any output.
	.EXAMPLE
		PS> Set-KrDefaultLogger -Logger $myLogger
		PS> $myLogger | Set-KrDefaultLogger
		Sets the specified logger as the current logger for the session.
	.EXAMPLE
		PS> $myLogger | Set-KrDefaultLogger
		Sets the specified logger as the current logger for the session.
	#>

	[KestrunRuntimeApi([KestrunApiContext]::Everywhere)]
    [CmdletBinding(SupportsShouldProcess = $true)]
	param(
		[Parameter(Mandatory = $true, ValueFromPipeline = $true)]
		[string]$Name
	)

	process {
		if ($PSCmdlet.ShouldProcess("Set the current logger for the session")) {
			[Kestrun.Logging.LoggerManager]::DefaultLogger = [Kestrun.Logging.LoggerManager]::Get($Name)
		}
	}
}