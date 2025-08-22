Import-Module ./src/PowerShell/Kestrun/Kestrun.psd1 -ErrorAction Stop

Write-Host 'Case 1: Strings Get,Post'
$opt1 = New-MapRouteOption -Property @{ Pattern='/test1'; HttpVerbs=@('Get','Post') }
$opt1.HttpVerbs.GetType().FullName
$opt1.HttpVerbs | ForEach-Object { " - $_" }

Write-Host "`nCase 2: Single string Put"
$opt2 = New-MapRouteOption -Property @{ Pattern='/test2'; HttpVerbs='Put' }
$opt2.HttpVerbs.GetType().FullName
$opt2.HttpVerbs | ForEach-Object { " - $_" }

Write-Host "`nCase 3: Enum values"
$opt3 = New-MapRouteOption -Property @{ Pattern='/test3'; HttpVerbs=@([Kestrun.Utilities.HttpVerb]::Delete,[Kestrun.Utilities.HttpVerb]::Get) }
$opt3.HttpVerbs.GetType().FullName
$opt3.HttpVerbs | ForEach-Object { " - $_" }

Write-Host "`nCase 4: Invalid value"
try {
  $null = New-MapRouteOption -Property @{ Pattern='/test4'; HttpVerbs=@('Get','BOGUS') }
  Write-Host 'ERROR: Expected failure for invalid verb.'
} catch {
  Write-Host "Caught expected error: $($_.Exception.Message)"
}
