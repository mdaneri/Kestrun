function Add-EnrichFromLogContext {
	<#
	.SYNOPSIS
		Enriches log events with properties from LogContext
	.DESCRIPTION
		Enriches log events with properties from LogContext. Use Push-LogContextProp to add properties.
	.PARAMETER LoggerConfig
		Instance of LoggerConfiguration that is already setup.
	.INPUTS
		Instance of LoggerConfiguration
	.OUTPUTS
		Instance of LoggerConfiguration
	.EXAMPLE
		PS> New-KrLogger | Add-EnrichFromLogContext | Add-KrSinkConsole | Register-KrLogger
	#>

	[Cmdletbinding()]
	param(
		[Parameter(Mandatory = $true, ValueFromPipeline = $true)]
		[Serilog.LoggerConfiguration]$LoggerConfig
	)

	process {
		$LoggerConfig.Enrich.FromLogContext()
	}
}