param([ValidateSet('Debug','Release')][string]$Configuration='Debug', [ValidateSet('Free')][string]$RionEdition='Free', [string]$CustomerFile='')
$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
dotnet build (Join-Path $root 'src/RionSuite/src/RionHub/RionHub.csproj') -c $Configuration -m:1 "-p:RionEdition=$RionEdition" "-p:RionCustomerFile=$CustomerFile"
if($LASTEXITCODE -ne 0){throw "Build failed ($LASTEXITCODE)."}
