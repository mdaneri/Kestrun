function Write-KrVerboseLog {
	<#
	.SYNOPSIS
		Writes Verbose log message
	.DESCRIPTION
		Write a log event with the Verbose level.
	.PARAMETER MessageTemplate
		Message template describing the event.
	.PARAMETER Logger
		Instance of Serilog.Logger. By default static property [Serilog.Log]::Logger is used.
	.PARAMETER Exception
		Exception related to the event.
	.PARAMETER ErrorRecord
		ErrorRecord related to the event.
	.PARAMETER PropertyValues
		Objects positionally formatted into the message template.
	.PARAMETER PassThru
		Outputs MessageTemplate populated with PropertyValues into pipeline.
	.INPUTS
		MessageTemplate - Message template describing the event.
	.OUTPUTS
		None or MessageTemplate populated with PropertyValues into pipeline if PassThru specified.
	.EXAMPLE
		PS> Write-KrVerboseLog 'Verbose log message' -PropertyValues 'value1', 'value2'
		This example logs a verbose message with two property values.
	.EXAMPLE
		PS> Write-KrVerboseLog -MessageTemplate 'Processed {@Position} in {Elapsed:000} ms.' -PropertyValues $position, $elapsedMs
		This example logs a verbose message with formatted properties.
	.EXAMPLE
		PS> Write-KrVerboseLog 'Error occurred' -Exception ([System.Exception]::new('Some exception'))
		This example logs a verbose message with an exception.
	.NOTES
		This function is part of the Kestrun logging framework and is used to log verbose messages.
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
		# Call the generic logging function with Verbose level
		Write-KrLog -LogLevel Verbose -MessageTemplate $MessageTemplate -Logger $Logger -Exception $Exception -ErrorRecord $ErrorRecord -PropertyValues $PropertyValues -PassThru:$PassThru
	}
}