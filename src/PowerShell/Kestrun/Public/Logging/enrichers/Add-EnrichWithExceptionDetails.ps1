# https://github.com/serilog/serilog-enrichers-environment
function Add-EnrichWithExceptionDetail {
	[Cmdletbinding()]
	[OutputType([Serilog.LoggerConfiguration])]
	param(
		[Parameter(Mandatory = $true, ValueFromPipeline = $true)]
		[Serilog.LoggerConfiguration]$LoggerConfig
	)

	process {
		$LoggerConfig = [Serilog.Exceptions.LoggerEnrichmentConfigurationExtensions]::WithExceptionDetails($LoggerConfig.Enrich)

		return $LoggerConfig
	}
}