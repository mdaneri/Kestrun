[Diagnostics.CodeAnalysis.SuppressMessageAttribute('PSAvoidUsingWriteHost', '')]
[CmdletBinding()]
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
---
"@ | Set-Content $topIndex -NoNewline
}

# Helper: title from first H1, else fallback; also escape any embedded quotes
function Get-TitleFromContent([string]$content, [string]$fallback) {
  $t = if ($content -match '(?m)^\s*#\s+(.+?)\s*$') { $Matches[1].Trim() } else { $fallback }
  return $t -replace '"', '\"'
}

# Helper: make a path relative to ApiRoot
$apiRootFull = (Resolve-Path $ApiRoot).Path.TrimEnd('\', '/')
function RelFromApi([string]$full) {
  $p = (Resolve-Path $full).Path
  return $p.Substring($apiRootFull.Length).TrimStart('\', '/')
}

# 1) Create index.md for each top-level namespace folder (e.g., global/)
$displayName = @{ "global" = "Global namespace" }

Get-ChildItem $ApiRoot -Directory | ForEach-Object {
  $nsFolder = $_.FullName
  $nsName = $_.Name
  $index = Join-Path $nsFolder "index.md"

  if (-not (Test-Path $index)) {
    $title = if ($displayName.ContainsKey($nsName)) { $displayName[$nsName] } else { $nsName }
    @"
---
layout: default
title: "$title"
parent: "$TopParent"
---
"@ | Set-Content $index -NoNewline
  }
}

# 2) Prepend front matter for every generated page that doesn't have it
Get-ChildItem $ApiRoot -Recurse -Filter *.md | ForEach-Object {
  $file = $_.FullName
  $content = Get-Content $file -Raw

  # Skip if already has front matter
  if ($content -match '^(?s)^\s*---\s*\n') { return }

  if ($_.PSChildName -eq "kestrun.md") {
    $front = @"
---
layout: default
title: C# API
nav_order: 1
parent: "C#"
---
"@
    Set-Content -Path (Join-Path -Path $_.DirectoryName -ChildPath 'index.md') -Value ($front + "`n" + $content) -NoNewline
    Remove-Item -Path $file -Force
    Write-Host "Updated index.md for C# API"

    return
  }

  $title = Get-TitleFromContent $content $_.BaseName

  # Determine namespace (first directory under api/)
  $relPath = RelFromApi $file
  $parts = $relPath -split '[\\/]+'
  $isInNamespace = ($parts.Length -ge 2)
  $namespace = if ($isInNamespace) { $parts[0] } else { $null }

  $front =
  if ($isInNamespace) {
    # Namespace index (api/<ns>/index.md)
    if ($parts.Length -eq 2 -and $parts[1].ToLower() -eq 'index.md') {
      @"
---
layout: default
title: "$($displayName[$namespace] ?? $namespace)"
parent: "$TopParent"
---
"@
    }
    else {
      @"
---
layout: default
title: "$title"
parent: "$namespace"
grand_parent: "$TopParent"
---
"@
    }
  }
  else {
    # Files directly under api/ (rare)
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

# 3) (Optional) ensure namespace index pages show first in their folder by adding nav_order=1
#    Uncomment if you want explicit ordering.
# Get-ChildItem $ApiRoot -Directory | ForEach-Object {
#   $idx = Join-Path $_.FullName 'index.md'
#   if (Test-Path $idx) {
#     $c = Get-Content $idx -Raw
#     if ($c -notmatch '(?m)^\s*nav_order:\s*\d+') {
#       $c = $c -replace '^(---\s*\r?\n)', "`$1nav_order: 1`r`n"
#       Set-Content $idx $c -NoNewline
#     }
#   }
# }
