# app.ps1

try {
    $ScriptPath   = Split-Path -Parent $MyInvocation.MyCommand.Path
    $ExamplesPath = Split-Path -Parent $ScriptPath
    $RootPath     = Split-Path -Parent $ExamplesPath

    # Import Kestrun module (from source if present, otherwise the installed module)
    $kestrunModule = "$RootPath/src/Kestrun.psm1"
    if (Test-Path $kestrunModule -PathType Leaf) {
        Import-Module $kestrunModule -Force -ErrorAction Stop
    }
    else {
        Import-Module -Name 'Kestrun' -MaximumVersion 2.99 -ErrorAction Stop
    }
}
catch {
    Write-Error "Failed to import Kestrun module: $_"
    exit 1
}

# Create the server
$server = New-KrServer -Name 'MyKestrunServer'

# Listen on port 5000 (HTTP)
Add-KrListener -Server $server -Port 5000  

# Seed a global counter (Visits) — injected as $Visits in every runspace
Set-KrGlobalVar -Name 'Visits' -Value 0

# ─────────────────────────────────────────────────────────────────────────────
# Route: GET /show
#   • $Visits is already injected as a PS variable
#   • Just read and write it back in the response
# ─────────────────────────────────────────────────────────────────────────────
Add-KrRoute -Server $server -Verbs Get -Path '/show' -ScriptBlock {
    # $Visits is available
    Write-KrTextResponse -inputObject "Visits so far: $Visits" -statusCode 200
}

# ─────────────────────────────────────────────────────────────────────────────
# Route: GET /visit
#   • Increment $Visits directly
#   • Persist the new value back into the global store
# ─────────────────────────────────────────────────────────────────────────────
Add-KrRoute -Server $server -Verbs Get -Path '/visit' -ScriptBlock {
    # increment the injected variable
    $Visits++
 
    Write-KrTextResponse -inputObject "Incremented to $Visits" -statusCode 200
}

# Start the server (blocking)
Start-KrServer -Server $server
