function Write-KrDebugLog {
	<#
	.SYNOPSIS
			Write a debug log message using the Kestrun logging framework.
	.DESCRIPTION
			This function writes a debug log message to the specified logger or the default logger.
	.PARAMETER MessageTemplate
			The message template to log. This can include placeholders for properties.
	.PARAMETER Logger
			The logger to use for logging. If not specified, the default logger is used.
	.PARAMETER Exception
			An optional exception to log along with the message.
	.PARAMETER ErrorRecord
			An optional error record to log. If provided, it will be logged as a fatal error.
	.PARAMETER PropertyValues
			An array of property values to include in the log message.
	.PARAMETER PassThru
			If specified, the function will return the logger object after logging.
	.EXAMPLE
			Write-KrDebugLog -MessageTemplate "Debug message with properties: {0}, {1}" -PropertyValues "value1", "value2"
			This example logs a debug message with two properties.
	.EXAMPLE
			Write-KrDebugLog -MessageTemplate "An error occurred" -Exception $exception -Logger $myLogger
			This example logs a debug message with an exception using a specific logger.
	.EXAMPLE
			Write-KrDebugLog -MessageTemplate "Fatal error" -ErrorRecord $errorRecord
			This example logs a fatal error message using an error record.
	.NOTES
			This function is part of the Kestrun logging framework and is used to log debug messages.
			It can be used in scripts and modules that utilize Kestrun for logging.
	#>

	[Cmdletbinding(DefaultParameterSetName = 'MsgTemp')]
	param(
		[Parameter(Mandatory = $true, Position = 0, ValueFromPipeline = $true, ParameterSetName = 'MsgTemp')]
		[Parameter(Mandatory = $false, Position = 0, ValueFromPipeline = $true, ParameterSetName = 'ErrRec')]
		[AllowEmptyString()]
		[string]$MessageTemplate,

		[Parameter(Mandatory = $false, ParameterSetName = 'MsgTemp')]
		[Parameter(Mandatory = $false, ParameterSetName = 'ErrRec')]
		[Serilog.ILogger]$Logger = [Serilog.Log]::Logger,

		[Parameter(Mandatory = $false, ParameterSetName = 'MsgTemp')]
		[Parameter(Mandatory = $false, ParameterSetName = 'ErrRec')]
		[AllowNull()]
		[System.Exception]$Exception,

		[Parameter(Mandatory = $true, ParameterSetName = 'ErrRec')]
		[Alias("ER")]
		[AllowNull()]
		[System.Management.Automation.ErrorRecord]$ErrorRecord,

		[Parameter(Mandatory = $false, ParameterSetName = 'MsgTemp')]
		[Parameter(Mandatory = $false, ParameterSetName = 'ErrRec')]
		[AllowNull()]
		[object[]]$PropertyValues,

		[Parameter(Mandatory = $false, ParameterSetName = 'MsgTemp')]
		[Parameter(Mandatory = $false, ParameterSetName = 'ErrRec')]
		[switch]$PassThru
	)

	process {
		Write-KrLog -LogLevel Debug -MessageTemplate $MessageTemplate -Logger $Logger -Exception $Exception -ErrorRecord $ErrorRecord -PropertyValues $PropertyValues -PassThru:$PassThru
	}
}