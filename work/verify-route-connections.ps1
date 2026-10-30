$ErrorActionPreference = 'Stop'
# Current data contracts; runtime flows are exercised by verify-dialogue-scene-progress.ps1.
& python (Join-Path $PSScriptRoot 'verify-dialogue-data.py') 'routes'
if ($LASTEXITCODE -ne 0) { throw 'Dialogue data verification failed.' }
