$publicRoot = Join-Path $PSScriptRoot '..' '..' 'src' 'Public'

Describe 'Kestrun PowerShell Functions' {
    BeforeAll {
        if (-not ('KestrumLib.GlobalVariables' -as [type])) {
            Add-Type -Language CSharp -TypeDefinition @"
namespace KestrumLib {
    public static class GlobalVariables {
        private static readonly System.Collections.Generic.Dictionary<string, object> Table = new();
        public static bool Define(string name, object value, bool readOnly) {
            if (readOnly && Table.ContainsKey(name)) return false;
            Table[name] = value; return true;
        }
        public static object Get(string name) => Table.TryGetValue(name, out var v) ? v : null;
        public static bool Remove(string name) => Table.Remove(name);
        public static bool TryGet(string name, out object value) => Table.TryGetValue(name, out value);
    }
}
"@
        }

        Get-ChildItem -Path "$publicRoot/*.ps1" | ForEach-Object { . $_.FullName }
    }

    AfterAll {
        Remove-Variable Response -Scope Script -ErrorAction SilentlyContinue
    }

    It 'Set-KrGlobalVar defines and retrieves values' {
        Set-KrGlobalVar -Name 'psTestVar' -Value @(1,2,3)
        (Get-KrGlobalVar -Name 'psTestVar').Count | Should -Be 3
    }

    It 'Remove-KrGlobalVar deletes value' {
        Set-KrGlobalVar -Name 'psToRemove' -Value @()
        Remove-KrGlobalVar -Name 'psToRemove'
        [KestrumLib.GlobalVariables]::TryGet('psToRemove', [ref]$null) | Should -BeFalse
    }

    It 'Resolve-KrPath returns absolute path' {
        $result = Resolve-KrPath -Path '.' -KestrunRoot
        [System.IO.Path]::IsPathRooted($result) | Should -BeTrue
    }

    It 'Write-KrTextResponse calls method on Response object' {
        $script:called = $null
        $script:Response = [pscustomobject]@{
            WriteTextResponse = { param($o,$s,$c) $script:called = "$o|$s|$c" }
        }
        Write-KrTextResponse -InputObject 'hi' -StatusCode 201 -ContentType 'text/plain'
        $script:called | Should -Be 'hi|201|text/plain'
        Remove-Variable Response -Scope Script
    }
}
