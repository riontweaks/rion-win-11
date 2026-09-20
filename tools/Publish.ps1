param([ValidateSet('Free')][string]$RionEdition='Free', [string]$CustomerFile='', [string]$OutputDirectory='dist/free')
$ErrorActionPreference = 'Stop'
if ($RionEdition -eq 'Paid' -and $OutputDirectory -notlike 'dist/customers/*') {
    throw 'Paid builds must use a private dist/customers output directory. Use Customer Builder.'
}
$root = Split-Path $PSScriptRoot -Parent
. (Join-Path $PSScriptRoot 'ReleaseSupport.ps1')
$source = Get-RionSourceManifest -Root $root
$sdk = & dotnet --version
if ($LASTEXITCODE -ne 0) { throw 'Could not read the .NET SDK version.' }
$releaseId = (Get-Date -Format 'yyyyMMdd-HHmmss') + '-' + [Guid]::NewGuid().ToString('N').Substring(0, 8)
$stage = Join-Path $root ('dist/.staging/' + $releaseId)
$raw = Join-Path $stage 'publish'
$package = Join-Path $stage 'package'
$output = Join-Path $root $OutputDirectory
$previous = Join-Path $root ('dist/previous/win-x64-' + $releaseId)
foreach ($path in @($raw, $package, $output, $previous)) { Assert-RionDistPath -Root $root -Path $path }
New-Item -ItemType Directory -Path $raw, $package -Force | Out-Null

& (Join-Path $PSScriptRoot 'Build.ps1') -Configuration Release -RionEdition $RionEdition -CustomerFile $CustomerFile
dotnet publish (Join-Path $root 'src/RionSuite/src/RionHub/RionHub.csproj') -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:EnableCompressionInSingleFile=false -o $raw -m:1 "-p:RionEdition=$RionEdition" "-p:RionCustomerFile=$CustomerFile"
if ($LASTEXITCODE -ne 0) { throw "Publish failed ($LASTEXITCODE). Previous release is unchanged; inspect $stage" }
$exe = Join-Path $raw 'Rion Win 11.exe'
if (!(Test-Path -LiteralPath $exe -PathType Leaf)) { throw 'The standalone executable was not created.' }
# Honor an isolated build output when the regular development executable is open.
$buildBase = if ($env:BaseOutputPath) { $env:BaseOutputPath } else { 'bin' }
if (-not [IO.Path]::IsPathRooted($buildBase)) { $buildBase = Join-Path (Join-Path $root 'src/RionSuite/src/RionHub') $buildBase }
$frameworks = @(Assert-RionRuntimeConfig -Path (Join-Path $buildBase 'Release/net8.0-windows/win-x64/Rion Win 11.runtimeconfig.json'))
Copy-Item -LiteralPath $exe -Destination $package
# Sign before hashing: a signature changes the file, so the recorded checksums and any
# antivirus result must describe the signed bytes that are actually distributed.
$signing = Invoke-RionSignFile -Path (Join-Path $package 'Rion Win 11.exe')
if ($signing.signed) { Write-Output "Signed: $($signing.subject)" }
else { Write-Output "Unsigned release. $($signing.reason)" }
foreach ($name in @('LICENSE', 'THIRD_PARTY_NOTICES.md')) {
    Copy-Item -LiteralPath (Join-Path $root $name) -Destination $package
}
$licenseDirectory = New-Item -ItemType Directory -Path (Join-Path $package 'licenses')
foreach ($file in $source.files | Where-Object { $_.path.StartsWith('licenses/') }) {
    $destination = Join-Path $package $file.path
    New-Item -ItemType Directory -Path (Split-Path $destination -Parent) -Force | Out-Null
    Copy-Item -LiteralPath (Join-Path $root $file.path) -Destination $destination
}
Copy-Item -LiteralPath (Join-Path $root 'src/RionAmdThinner/src/Shared/7-Zip/License.txt') `
    -Destination (Join-Path $licenseDirectory.FullName '7-Zip-LICENSE.txt')

$after = Get-RionSourceManifest -Root $root
if ($source.sha256 -ne $after.sha256) {
    throw "Source changed during the build. Previous release is unchanged; rebuild a consistent snapshot. Inspect $stage"
}
$artifacts = @(Get-ChildItem -LiteralPath $package -File -Recurse | Sort-Object FullName | ForEach-Object {
    [ordered]@{
        path = $_.FullName.Substring($package.Length + 1).Replace('\', '/')
        bytes = $_.Length
        sha256 = (Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash
    }
})
[ordered]@{
    schemaVersion = 1
    edition = $RionEdition
    createdUtc = [DateTime]::UtcNow.ToString('o')
    sdkVersion = "$sdk".Trim()
    configuration = 'Release'
    runtimeIdentifier = 'win-x64'
    selfContained = $true
    singleFile = $true
    bundledFrameworks = $frameworks
    signing = $signing
    testsRunByPublish = $false
    source = $source
    artifacts = $artifacts
} | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath (Join-Path $package 'release-manifest.json') -Encoding UTF8
Get-ChildItem -LiteralPath $package -File -Recurse | Sort-Object FullName | ForEach-Object {
    (Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash + '  ' +
        $_.FullName.Substring($package.Length + 1).Replace('\', '/')
} | Set-Content -LiteralPath (Join-Path $package 'SHA256SUMS.txt') -Encoding ascii

# Preserve previous output wholesale; never merge a fresh release with stale files.
foreach ($path in @($package, $output, $previous)) { Assert-RionDistPath -Root $root -Path $path }
$hadPrevious = Test-Path -LiteralPath $output
if ($hadPrevious) {
    New-Item -ItemType Directory -Path (Split-Path $previous -Parent) -Force | Out-Null
    Move-Item -LiteralPath $output -Destination $previous
}
New-Item -ItemType Directory -Path (Split-Path $output -Parent) -Force | Out-Null
try { Move-Item -LiteralPath $package -Destination $output }
catch {
    if ($hadPrevious -and !(Test-Path -LiteralPath $output)) {
        Assert-RionDistPath -Root $root -Path $previous
        Assert-RionDistPath -Root $root -Path $output
        Move-Item -LiteralPath $previous -Destination $output
    }
    throw
}
Get-Item -LiteralPath (Join-Path $output 'Rion Win 11.exe') | Select-Object FullName, Length, LastWriteTime
Write-Output "Release manifest and checksums: $output. Raw output and symbols retained: $raw"
