[Diagnostics.CodeAnalysis.SuppressMessageAttribute('PSAvoidUsingWriteHost', '')]
[CmdletBinding()]
param(
  # Path to the module manifest (psd1) or psm1
  [string]$ModulePath = "./src/PowerShell/Kestrun/Kestrun.psm1",
  # Where Markdown will be written
  [string]$OutDir = "./docs/cmdlets",
  # Optional culture for XML help (not required for web site)
  [string]$XmlCulture = "en-US",
  # Create/refresh XML help too?
  [switch]$EmitXmlHelp,
  [switch]$Clean
)

$ErrorActionPreference = 'Stop'

if ($Clean) {
    Write-Host "Cleaning PlatyPS..."
    Remove-Item -Path $OutDir -Recurse -Force -ErrorAction SilentlyContinue
    return
}
# Pin to a modern PlatyPS (v2+ is fine). Remove -RequiredVersion to float latest.
if (-not (Get-Module -ListAvailable -Name PlatyPS)) {
    Write-Host "Installing PlatyPS..."
    Install-Module PlatyPS -Scope CurrentUser -Force -AllowClobber
} else {
    Write-Host "PlatyPS is already installed."
}

New-Item -ItemType Directory -Force -Path $OutDir | Out-Null
if (Test-Path -Path "$OutDir/about_.md") {
    Write-Host "Clearing existing help in $OutDir/about_.md"
    Remove-Item -Path "$OutDir/about_.md" -Force
}
New-MarkdownAboutHelp -OutputFolder $OutDir

Write-Host "Importing module: $ModulePath"
Import-Module $ModulePath -Force
New-KrServer -Name "Docs"
Remove-KrServer -Name "Docs" -Force


Write-Host "Generating Markdown help..."
# Create or update Markdown help
if (Test-Path (Join-Path $OutDir "Kestrun.md")) {
    Write-Host "Updating existing markdown help in $OutDir"
    Update-MarkdownHelp -Path $OutDir -Force
} else {
    Write-Host "Creating markdown help in $OutDir"
    New-MarkdownHelp -Module (Get-Module -Name Kestrun) -OutputFolder $OutDir 
}

# (Optional) emit external help XML to ship in your module
if ($EmitXmlHelp) {
  $xmlOut = Join-Path $OutDir $XmlCulture
  New-Item -ItemType Directory -Force -Path $xmlOut | Out-Null
  Write-Host "Generating external help XML…"
  New-ExternalHelp -Path $OutDir -OutputPath $xmlOut -Force
}
Write-Host "Done. Markdown at $OutDir"
