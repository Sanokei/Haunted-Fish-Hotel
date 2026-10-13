$ErrorActionPreference = 'Stop'
Push-Location (Resolve-Path (Join-Path $PSScriptRoot '../..')).Path
try {
 python Tools/PlayerTests/VerifyCartArtwork.py
 if ($LASTEXITCODE -ne 0) { throw 'Artwork coverage failed' }
 python Tools/PlayerTests/VerifyNativeCartEvidence.py
 if ($LASTEXITCODE -ne 0) { throw 'Native evidence stale or failed' }
} finally { Pop-Location }
