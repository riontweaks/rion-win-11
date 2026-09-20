$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
Push-Location $root
try {
 & ./tools/Build.ps1 -Configuration Release
 dotnet run --project src/RionSuite/tests/FreeSmoke -c Release -p:RionEdition=Free
 if($LASTEXITCODE -ne 0){throw 'Free smoke tests failed.'}
 dotnet build src/RionSuite/src/RionHub/RionHub.csproj --no-restore -p:RionEdition=Paid -m:1 *> artifacts/rejected-paid-build.log
 if($LASTEXITCODE -eq 0){throw 'Paid override was accepted.'}
 dotnet build src/RionSuite/src/RionHub/RionHub.csproj --no-restore -p:RionEdition=Development -m:1 *> artifacts/rejected-development-build.log
 if($LASTEXITCODE -eq 0){throw 'Development override was accepted.'}
 $global:LASTEXITCODE=0
 Write-Output 'PASS: both non-Free overrides rejected.'
} finally {Pop-Location}