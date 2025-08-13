[Diagnostics.CodeAnalysis.SuppressMessageAttribute('PSAvoidUsingWriteHost', '')]
[CmdletBinding()]
param(
    # Path to the module manifest (psd1) or psm1
    [string]$ModulePath = "./src/PowerShell/Kestrun/Kestrun.psm1",
    # Where Markdown will be written
    [string]$OutDir = "./docs/pwsh/cmdlets",
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
}
else {
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
if (Test-Path (Join-Path $OutDir "index.md")) {
    Write-Host "Updating existing markdown help in $OutDir"
    Update-MarkdownHelp -Path $OutDir -Force
}
else {
    Write-Host "Creating markdown help in $OutDir"
    New-MarkdownHelp -Module (Get-Module -Name Kestrun) -OutputFolder $OutDir


    $index_md = @"
---
layout: default
title: PowerShell Cmdlets
has_children: true
nav_order: 10
# parent defaults to "All Pages" via _config.yml
---

# PowerShell Cmdlets

<ul>
{% assign here = 'docs/pwsh/cmdlets/' %}

{%- comment -%} Render Markdown pages that Jekyll processes {%- endcomment -%}
{% for p in site.pages %}
  {% if p.path contains here and p.name != 'index.md' and p.name != 'index.html' %}
    <li><a href="{{ p.url | relative_url }}">{{ p.title | default: p.name }}</a></li>
  {% endif %}
{% endfor %}

{%- comment -%} Also list plain .md files (without front matter) if any {%- endcomment -%}
{% for f in site.static_files %}
  {% if f.path contains here and f.extname == '.md' and f.name != 'index.md' %}
    <li><a href="{{ f.path | relative_url }}">{{ f.name }}</a></li>
  {% endif %}
{% endfor %}
</ul>
"@

    Set-Content -Path (Join-Path $OutDir "index.md") -Value $index_md -Encoding UTF8
}

 

# Normalize cmdlet pages for Just the Docs
$files = Get-ChildItem $OutDir -Recurse -Filter *.md | Sort-Object Name
$i = 1
foreach ($f in $files) {
    if ($f.Name -ieq 'index.md') { continue }

    $raw = Get-Content $f.FullName -Raw

    # Title from first H1 or filename
    $title = ($raw -split "`n" | Where-Object { $_ -match '^\s*#\s+(.+)$' } | Select-Object -First 1)
    $title = if ($title) { $title -replace '^\s*#\s+', '' } else { [IO.Path]::GetFileNameWithoutExtension($f.Name) }

    if ($raw -notmatch '^\s*---\s*$') {
        @"
---
layout: default
parent: PowerShell Cmdlets
title: $title
nav_order: $i
render_with_liquid: false
---
$raw
"@ | Set-Content $f.FullName -NoNewline
    }
    else {
        # If it already has front matter, ensure the key bits are present
        $lines = $raw -split "`n"
        $endIdx = ($lines | Select-String -SimpleMatch '---' -AllMatches).Matches[1].Index
        $head = $lines[0..$endIdx]
        $body = $lines[($endIdx + 1)..($lines.Length - 1)]

        $ensure = @()
        if ($head -notcontains 'layout: default') { $ensure += 'layout: default' }
        if ($head -notmatch '^parent:\s') { $ensure += 'parent: PowerShell Cmdlets' }
        if ($head -notmatch '^title:\s') { $ensure += "title: $title" }
        if ($head -notmatch '^nav_order:\s') { $ensure += "nav_order: $i" }
        if ($head -notcontains 'render_with_liquid: false') { $ensure += 'render_with_liquid: false' }

        $newHead = @($head[0]) + ($head[1..($head.Count - 1)] + $ensure | Select-Object -Unique)
        ($newHead + $body) -join "`n" | Set-Content $f.FullName -NoNewline
    }

    $i++
}



# (Optional) emit external help XML to ship in your module
if ($EmitXmlHelp) {
    $xmlOut = Join-Path $OutDir $XmlCulture
    New-Item -ItemType Directory -Force -Path $xmlOut | Out-Null
    Write-Host "Generating external help XML…"
    New-ExternalHelp -Path $OutDir -OutputPath $xmlOut -Force
}
Write-Host "Done. Markdown at $OutDir"
