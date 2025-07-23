 
try {
    # Get the path of the current script
    # This allows the script to be run from any location
    $ScriptPath = (Split-Path -Parent -Path $MyInvocation.MyCommand.Path)
    # Determine the script path and Kestrun module path
    $powerShellExamplesPath = (Split-Path -Parent ($ScriptPath))
    # Determine the script path and Kestrun module path
    $examplesPath = (Split-Path -Parent ($powerShellExamplesPath))
    # Get the parent directory of the examples path
    # This is useful for locating the Kestrun module
    $kestrunPath = Split-Path -Parent -Path $examplesPath
    # Construct the path to the Kestrun module
    # This assumes the Kestrun module is located in the src/PowerShell/Kestr
    $kestrunModulePath = "$kestrunPath/src/PowerShell/Kestrun/Kestrun.psm1"

    # Import the Kestrun module from the source path if it exists, otherwise from installed modules
    if (Test-Path -Path $kestrunModulePath -PathType Leaf) {
        Import-Module $kestrunModulePath -Force -ErrorAction Stop
    }
    else {
        Import-Module -Name 'Kestrun' -MaximumVersion 2.99 -ErrorAction Stop
    }
}
catch {
    Write-Error "Failed to import Kestrun module: $_"
    Write-Error "Ensure the Kestrun module is installed or the path is correct."
    exit 1
}


$server = New-KrServer -Name "MyKestrunServer"

# Level switch allows you to switch minimum logging level
$levelSwitch = New-KrLevelSwitch -MinimumLevel Verbose

$l0 = New-KrLogger |
Set-KrMinimumLevel -Value Verbose -ToPreference |
Add-EnrichWithEnvironment |
Add-EnrichWithExceptionDetail |
Add-EnrichFromLogContext |
Add-KrSinkPowerShell |
Add-KrSinkConsole -OutputTemplate "[{MachineName} {Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}" | 
Start-KrLogger -PassThru -SetAsDefault

Write-KrInfoLog -MessageTemplate 'Some default log'

Close-KrLogger -Logger $l0

# Setup new logger
$logger1 = New-KrLogger |
Set-KrMinimumLevel -Value Verbose |
Add-EnrichWithEnvironment |
Add-EnrichWithExceptionDetail |
Add-KrSinkFile -Path ".\logs\test-.txt" -RollingInterval Hour -OutputTemplate '{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {Message:lj}{NewLine}{Exception} {Properties:j}{NewLine}' |
Add-KrSinkConsole -OutputTemplate "[{MachineName} {Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}" |
Start-KrLogger -PassThru

$logger2 = Start-KrLogger -FilePath ".\logs\test2-.txt" -Console -PassThru -MinimumLevel Verbose

# Write-KrVerboseLog "test verbose"
Write-KrDebugLog -MessageTemplate "test debug asd" -Logger $logger1

Set-KrLogger -Logger $logger2
Write-KrInfoLog -MessageTemplate $null
Write-KrInfoLog -MessageTemplate ''
Write-KrInfoLog -MessageTemplate 'asd {0} - {1}' -PropertyValues $null, '' -Exception $null

Write-KrDebugLog -MessageTemplate "test debug asdasdsad"

Write-KrWarningLog "test warning" -Logger $logger1

Set-KrLogger -Logger $logger1

Write-KrInfoLog "test info"

Write-KrErrorLog -MessageTemplate "test error {asd}, {num}, {@levelSwitch}" -PropertyValues "test1", 123, $levelSwitch -Logger $logger2

try {
    Get-Content -Path 'asd' -ErrorAction Stop
}
catch {
    Write-KrFatalLog -ErrorRecord $_ -MessageTemplate 'Error while reading file!'
}

Close-KrLogger