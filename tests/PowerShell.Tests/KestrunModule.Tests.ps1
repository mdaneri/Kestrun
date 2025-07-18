$modulePath = Join-Path $PSScriptRoot '..' '..' 'src' 'Kestrun.psd1'

Describe 'Kestrun PowerShell Module' {
    BeforeAll {
        Import-Module $modulePath -Force
    }

    AfterAll {
        Remove-Module Kestrun -Force
    }

    It 'Exports Set-KrGlobalVar command' {
        Get-Command Set-KrGlobalVar | Should -Not -BeNullOrEmpty
    }

    It 'Can define and retrieve a global variable' {
        Set-KrGlobalVar -Name 'psTestVar' -Value @(1,2,3)
        (Get-KrGlobalVar -Name 'psTestVar').Count | Should -Be 3
    }

    It 'Can remove a global variable' {
        Set-KrGlobalVar -Name 'psToRemove' -Value @()
        Remove-KrGlobalVar -Name 'psToRemove'
        [KestrumLib.GlobalVariables]::TryGet('psToRemove', [ref]$null) | Should -BeFalse
    }
}
