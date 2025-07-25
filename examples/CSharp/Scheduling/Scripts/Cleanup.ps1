# Nightly cleanup – just touch the Visits counter for demo
if ($null -ne $Visits) {
    $Visits["Count"] = 0
    Write-Host "[$(Get-Date -f o)] ♻️  Reset Visits to 0."
}
