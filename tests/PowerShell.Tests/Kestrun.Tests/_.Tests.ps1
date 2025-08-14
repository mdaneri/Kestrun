 
[Diagnostics.CodeAnalysis.SuppressMessageAttribute('PSUseDeclaredVarsMoreThanAssignments', '')]
param()
BeforeDiscovery {
    try {
        $path = $PSCommandPath

        $kestrunPath = Join-Path -Path (Split-Path -Parent (Split-Path -Parent (Split-Path -Parent (Split-Path -Parent $path)))) -ChildPath 'src' -AdditionalChildPath "PowerShell", "Kestrun"

        # Import the Kestrun module from the source path if it exists, otherwise from installed modules
        if (Test-Path -Path "$($kestrunPath)/Kestrun.psm1" -PathType Leaf) {
            Import-Module "$($kestrunPath)/Kestrun.psm1" -Force -ErrorAction Stop
        }
        else {
            throw "Kestrun module not found in source path: $kestrunPath"
        }
        New-KrServer -Name "Docs"
        Remove-KrServer -Name "Docs" -Force
        $psDataFile = Import-PowerShellDataFile "$kestrunPath/Kestrun.psd1"
        $funcs = $psDataFile.FunctionsToExport
    }
    catch {
        Write-Error "Failed to import Kestrun module: $_"
        Write-Error "Ensure the Kestrun module is installed or the path is correct."
        exit 1
    }
 
}
Describe 'Exported Functions' {
    It 'Have function [<_>] each parameter documented' -ForEach $funcs { 
        $params = (Get-Help -Name $_ -Detailed).parameters.parameter
        foreach ($param in $params) {
            $param.Description | Should -not -BeNullOrEmpty -Because "Parameter '$($param.Name)' of function '$($_)' is not documented."
        }
    }

}
 