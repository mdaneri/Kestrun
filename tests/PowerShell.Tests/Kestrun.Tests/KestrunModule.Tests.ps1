 
 
[Diagnostics.CodeAnalysis.SuppressMessageAttribute('PSUseDeclaredVarsMoreThanAssignments', '')]
param()
BeforeAll {
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
    }
    catch { 
        Write-Error "Failed to import Kestrun module: $_"
        Write-Error "Ensure the Kestrun module is installed or the path is correct."
        exit 1
    }

}
 
Describe 'Kestrun PowerShell Functions' {
   

    AfterAll {
        Remove-Variable Response -Scope Script -ErrorAction SilentlyContinue
    }

    It 'Set-KrSharedState defines and retrieves values' {
        Set-KrSharedState -Name 'psTestVar' -Value @(1, 2, 3)
        (Get-KrSharedState -Name 'psTestVar').Count | Should -Be 3
    }

    It 'Remove-KrSharedState deletes value' {
        $value= @()
        Set-KrSharedState -Name 'psToRemove' -Value @(1,2,3)
        [Kestrun.SharedState]::TryGet[object]('psToRemove', [ref]$value)| Should -BeTrue
        Remove-KrSharedState -Name 'psToRemove'
        [Kestrun.SharedState]::TryGet[object]('psToRemove', [ref]$value) | Should -BeFalse
    }

    It 'Resolve-KrPath returns absolute path' {
        $result = Resolve-KrPath -Path '.' -KestrunRoot
        [System.IO.Path]::IsPathRooted($result) | Should -BeTrue
    }

    <# It 'Write-KrTextResponse calls method on Response object' {
        $called = $null
        $Response = [pscustomobject]@{
            WriteTextResponse = { param($o, $s, $c) $called = "$o|$s|$c" }
        }
        Write-KrTextResponse -InputObject 'hi' -StatusCode 201 -ContentType 'text/plain'
        $script:called | Should -Be 'hi|201|text/plain'
        Remove-Variable Response -Scope Script
    }#>
}
