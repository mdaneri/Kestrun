function Set-KrLogger {
	<#
	.SYNOPSIS
		Sets the logger for the current session.
	.DESCRIPTION
		Sets the specified logger as the current logger for the session.
	.PARAMETER Logger
		Instance of Serilog.ILogger to set as the current logger.
	.INPUTS
		Instance of Serilog.ILogger
	.OUTPUTS
		None. This cmdlet does not return any output.
	.EXAMPLE
		PS> Set-KrLogger -Logger $myLogger
		PS> $myLogger | Set-KrLogger
		Sets the specified logger as the current logger for the session.
	.EXAMPLE
		PS> $myLogger | Set-KrLogger
		Sets the specified logger as the current logger for the session.
	#>

	[CmdletBinding(SupportsShouldProcess = $true)]
	param(
		[Parameter(Mandatory = $true, ValueFromPipeline = $true)]
		[Serilog.ILogger]$Logger
	)

	process {
		if ($PSCmdlet.ShouldProcess("Set the current logger for the session")) {
			[Serilog.Log]::Logger = $Logger
		}
	}
}