function Convert-LogFunctions {
	<#
	.SYNOPSIS
		Converts default cmdlets in given script file into logger methods
	.DESCRIPTION
		Converts default cmdlets in given script file into logger methods. For example Write-Information into Write-KrInfoLog, Write-Error into Write-KrErrorLog and so on.
	.PARAMETER FilePath
		Path to a script file in wich functions will be converted.
	.INPUTS
		string containing path to a script file
	.OUTPUTS
		None
	.EXAMPLE
		PS>  Convert-LogFunctions -FilePath C:\myscript.ps1
	#>

	[Cmdletbinding()]
	param(
		[Parameter(Mandatory = $true, ValueFromPipeline = $true)]
		[string]$FilePath
	)

	Write-Debug "Converting $FilePath"

	$script = Get-Content $FilePath
	$script | Foreach-Object {
		$_ -replace 'Write-Verbose -Message', 'Write-KrVerboseLog -MessageTemplate' `
		   -replace 'Write-Verbose ', 'Write-KrVerboseLog ' `
		   -replace 'Write-Debug -Message', 'Write-KrDebugLog -MessageTemplate' `
		   -replace 'Write-Debug ', 'Write-KrDebugLog ' `
		   -replace 'Write-Information -MessageData', 'Write-KrInfoLog -MessageTemplate' `
		   -replace 'Write-Information ', 'Write-KrInfoLog ' `
		   -replace 'Write-Host -Object', 'Write-KrInfoLog -MessageTemplate' `
		   -replace 'Write-Host ', 'Write-KrInfoLog ' `
		   -replace 'Write-Warning -Message', 'Write-KrWarningLog -MessageTemplate' `
		   -replace 'Write-Warning ', 'Write-KrWarningLog ' `
		   -replace 'Write-Error -Message', 'Write-KrErrorLog -MessageTemplate' `
		   -replace 'Write-Error ', 'Write-KrErrorLog ' `
		} | Set-Content $FilePath

	Write-Debug "$FilePath successfully converted"
}