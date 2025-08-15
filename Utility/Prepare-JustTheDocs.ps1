param(
  [string]$ApiRoot = "docs/cs/api",
  [string]$TopParent = "C# API"
)

$ErrorActionPreference = "Stop"

if (-not (Test-Path $ApiRoot)) {
  throw "API root not found: $ApiRoot"
}

# Ensure top index exists
$topIndex = "docs/cs/index.md"
if (-not (Test-Path $topIndex)) {
@"
---
layout: default
title: $TopParent
nav_order: 30
permalink: /cs/
has_children: true
---
"@ | Set-Content $topIndex -NoNewline
}

function Get-TitleFromContent([string]$content, [string]$fallback) {
  if ($content -match '(?m)^\s*#\s+(.+?)\s*$') { return $Matches[1].Trim() }
  return $fallback
}

Get-ChildItem $ApiRoot -Recurse -Filter *.md | ForEach-Object {
  $file = $_.FullName
  $rel  = Resolve-Path $file | Split-Path -NoQualifier
  $content = Get-Content $file -Raw

  # Skip if already has front matter
  if ($content -match '^(?s)^\s*---\s*\n') { return }

  $title = Get-TitleFromContent $content $_.BaseName

  # Determine namespace (first directory under api/)
  $relFromApi = $file.Substring((Resolve-Path $ApiRoot).Path.Length).TrimStart('\','/')
  $parts = $relFromApi -split '[\\/]'
  $isInNamespace = $parts.Length -ge 2
  $namespace = if ($isInNamespace) { $parts[0] } else { $null }

  $front =
    if ($isInNamespace) {
      # If this is the namespace index page (api/<ns>/index.md)
      if ($parts.Length -eq 2 -and $parts[1].ToLower() -eq 'index.md') {
@"
---
layout: default
title: "$namespace"
parent: "$TopParent"
has_children: true
---
"@
      } else {
@"
---
layout: default
title: "$title"
parent: "$namespace"
grand_parent: "$TopParent"
---
"@
      }
    } else {
      # Files directly under api/ (rare with xmldocmd, but handle gracefully)
@"
---
layout: default
title: "$title"
parent: "$TopParent"
---
"@
    }

  Set-Content -Path $file -Value ($front + "`n" + $content) -NoNewline
}
