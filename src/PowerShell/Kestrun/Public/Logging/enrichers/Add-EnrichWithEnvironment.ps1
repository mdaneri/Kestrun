function Add-EnrichWithEnvironment {
    [CmdletBinding()]
	[OutputType([Serilog.LoggerConfiguration])]
    param(
        [Parameter(Mandatory = $true, ValueFromPipeline = $true)]
        [Serilog.LoggerConfiguration]$loggerConfig,
        [Parameter(Mandatory = $false)]
        [switch]$UserName,
        [Parameter(Mandatory = $false)]
        [switch]$MachineName
    )

    process {
        $hasEnricher = $false

        # Only add if UserName is true or both are false (default: both on)
        if ($UserName -or (-not $UserName.IsPresent -and -not $MachineName.IsPresent)) {
            $loggerConfig = [Serilog.EnvironmentLoggerConfigurationExtensions]::WithEnvironmentUserName($loggerConfig.Enrich)
            $hasEnricher = $true
        }
        # Only add if MachineName is true or both are false (default: both on)
        if ($MachineName -or (-not $UserName.IsPresent -and -not $MachineName.IsPresent)) {
            $loggerConfig = [Serilog.EnvironmentLoggerConfigurationExtensions]::WithMachineName($loggerConfig.Enrich)
            $hasEnricher = $true
        }

        if (-not $hasEnricher) {
            Write-Verbose "No environment enrichers added."
        }

        return $loggerConfig
    }
}
