# Shared public source inventory for packaging and build provenance.
function Get-RionSourceFiles {
    param([Parameter(Mandatory)][string]$Root)

    $files = [System.Collections.Generic.List[string]]::new()
    # Public documentation is deliberate: private handoffs and local reviews stay out.
    foreach ($name in @(
        'Directory.Build.targets', 'README.md', 'LICENSE', 'THIRD_PARTY_NOTICES.md', 'CONTRIBUTING.md',
        'global.json', '.gitignore', '.gitattributes', 'Rion Win 11.sln',
        'tools/Build.ps1', 'tools/Test.ps1', 'tools/Publish.ps1', 'tools/Build-Distribution.ps1',
        'Customer Builder/Build-Customer.ps1', 'Customer Builder/Build-Customer.cmd', 'Customer Builder/Edit-Customer.cmd', 'Customer Builder/customer-template.txt', 'Customer Builder/README.md',
        'tools/Read-RionPowerValues.py', 'tools/New-FreeEdition.ps1', 'docs/EDITIONS.md', 'docs/CHANGELOG-20260919.md',
        'tools/Export-Source.ps1', 'tools/ReleaseSupport.ps1',
        'tools/Build-Installer.ps1', 'tools/installer/Rion.nsi',
        'tools/Build-Web-Installer.ps1', 'tools/installer/Download-Payload.ps1',
        'tools/tests/ReleasePackaging.Tests.ps1',
        'tools/tests/CustomerBuilder.Tests.ps1',
        'tools/tests/WebInstaller.Tests.ps1',
        'tools/tests/Typography.Tests.ps1',
        'docs/RELEASE.md', 'docs/TESTING.md', 'docs/SECURITY.md', 'docs/RECOVERY.md', 'docs/IMPLEMENTATION.md', 'docs/GPU-CONTROL-COVERAGE.md', 'docs/NVIDIA-DRS-MAPPING-AUDIT.md', 'docs/UI-BIOS-WORKFLOWS.md', '.github/workflows/build.yml'
    )) {
        $path = Join-Path $Root $name
        if (!(Test-Path -LiteralPath $path -PathType Leaf)) { continue }
        if ((Get-Item -LiteralPath $path -Force).Attributes -band [IO.FileAttributes]::ReparsePoint) {
            throw "Public source file cannot be a link: $name"
        }
        $files.Add($path)
    }

    $skip = @('bin', 'obj', 'artifacts', 'TestResults', 'publish', 'publish-tmp',
        'dist', 'packages', 'node_modules', '.git', '.vs', '.idea', '.workspace',
        'research', 'third_party', 'decompiled', 'SCEWIN')
    $pending = [System.Collections.Generic.Stack[string]]::new()
    foreach ($name in @('src', 'licenses')) { $pending.Push((Join-Path $Root $name)) }
    while ($pending.Count) {
        $directory = Get-Item -LiteralPath $pending.Pop() -Force
        if ($directory.Attributes -band [IO.FileAttributes]::ReparsePoint) {
            throw "Source directory cannot be a link: $($directory.FullName)"
        }
        foreach ($item in Get-ChildItem -LiteralPath $directory.FullName -Force) {
            if ($item.Attributes -band [IO.FileAttributes]::ReparsePoint) { continue }
            if ($item.PSIsContainer) {
                if ($item.Name -notin $skip) { $pending.Push($item.FullName) }
                continue
            }
            if ($item.Name -match '(?i)(\.bak($|[.-])|\.orig$|\.tmp$|^test-output.*\.txt$|\.log$|\.user$|\.suo$|\.pfx$|\.p12$|\.pem$|\.key$|\.sync-tmp$|^\.env($|\.))') { continue }
            $files.Add($item.FullName)
        }
    }
    $files | Sort-Object
}

function Get-RionSourceManifest {
    param([Parameter(Mandatory)][string]$Root)

    $entries = @(Get-RionSourceFiles -Root $Root | ForEach-Object {
        [ordered]@{
            path = $_.Substring($Root.Length + 1).Replace('\', '/')
            sha256 = (Get-FileHash -LiteralPath $_ -Algorithm SHA256).Hash
        }
    })
    $text = ($entries | ForEach-Object { $_.sha256 + '  ' + $_.path }) -join "`n"
    $algorithm = [Security.Cryptography.SHA256]::Create()
    try { $digest = [BitConverter]::ToString($algorithm.ComputeHash([Text.Encoding]::UTF8.GetBytes($text))).Replace('-', '') }
    finally { $algorithm.Dispose() }
    [ordered]@{ sha256 = $digest; files = $entries }
}

# Authenticode signing for the packaged executable.
#
# Signing establishes publisher identity and file integrity; it does not establish
# why an antivirus engine flags a file or guarantee an undetected verdict.
# Signing material never lives in the source tree:
# the certificate is named by environment variable and read from outside the repository.
#
#   RION_SIGN_DLIB          Path to a cloud signing provider library (Azure Trusted
#                           Signing, DigiCert KeyLocker, SSL.com eSigner). Since June 2023
#                           a publicly trusted code signing key must live on hardware or in
#                           a qualified HSM, so this is the usual path for a bought
#                           certificate; the provider authenticates, not this script.
#   RION_SIGN_DLIB_METADATA Path to the provider's JSON metadata, required with the dlib.
#   RION_SIGN_THUMBPRINT    Thumbprint of a certificate already in the user's store
#                           (use this for a hardware USB token).
#   RION_SIGN_CERT          Path to a .pfx. Only usable for a test or internal certificate:
#                           a bought certificate is no longer issued as a bare file.
#   RION_SIGN_PASSWORD      Password for a .pfx, when it has one.
#   RION_SIGN_TIMESTAMP     RFC 3161 timestamp URL; defaults to DigiCert's.
#   RION_SIGNTOOL           Explicit signtool.exe path, when it is not on PATH.
#
# Signing is skipped, not failed, when nothing is configured, so an unsigned local build
# still works. Configured-but-broken signing fails the release instead of quietly shipping
# an unsigned file.
function Invoke-RionSignFile {
    param([Parameter(Mandatory)][string]$Path)

    $certificate = $env:RION_SIGN_CERT
    $thumbprint = $env:RION_SIGN_THUMBPRINT
    $dlib = $env:RION_SIGN_DLIB
    if (!$certificate -and !$thumbprint -and !$dlib) {
        return [ordered]@{ signed = $false; reason = 'No signing certificate configured (RION_SIGN_DLIB / RION_SIGN_THUMBPRINT / RION_SIGN_CERT unset).' }
    }

    $signtool = Get-RionSignTool
    $timestamp = if ($env:RION_SIGN_TIMESTAMP) { $env:RION_SIGN_TIMESTAMP } else { 'http://timestamp.digicert.com' }

    # SHA-256 digest and an RFC 3161 timestamp, so the signature outlives the certificate.
    $arguments = @('sign', '/fd', 'SHA256', '/td', 'SHA256', '/tr', $timestamp)
    if ($dlib) {
        if (!(Test-Path -LiteralPath $dlib -PathType Leaf)) { throw "Signing provider library not found: $dlib" }
        $metadata = $env:RION_SIGN_DLIB_METADATA
        if (!$metadata) { throw 'RION_SIGN_DLIB requires RION_SIGN_DLIB_METADATA.' }
        if (!(Test-Path -LiteralPath $metadata -PathType Leaf)) { throw "Signing provider metadata not found: $metadata" }
        $arguments += @('/v', '/dlib', $dlib, '/dmdf', $metadata)
    }
    elseif ($thumbprint) { $arguments += @('/sha1', $thumbprint) }
    else {
        if (!(Test-Path -LiteralPath $certificate -PathType Leaf)) { throw "Signing certificate not found: $certificate" }
        $arguments += @('/f', $certificate)
        if ($env:RION_SIGN_PASSWORD) { $arguments += @('/p', $env:RION_SIGN_PASSWORD) }
    }
    $arguments += $Path

    & $signtool @arguments | Write-Verbose
    if ($LASTEXITCODE -ne 0) { throw "signtool failed ($LASTEXITCODE) for $Path" }

    & $signtool @('verify', '/pa', '/all', $Path) | Write-Verbose
    if ($LASTEXITCODE -ne 0) { throw "Authenticode verification failed after signing $Path" }

    $signature = Get-AuthenticodeSignature -LiteralPath $Path
    if ($signature.Status -ne 'Valid') { throw "Authenticode status after signing is $($signature.Status) for $Path" }
    [ordered]@{
        signed = $true
        subject = "$($signature.SignerCertificate.Subject)"
        thumbprint = "$($signature.SignerCertificate.Thumbprint)"
        timestampUrl = $timestamp
    }
}

function Get-RionSignTool {
    if ($env:RION_SIGNTOOL) {
        if (!(Test-Path -LiteralPath $env:RION_SIGNTOOL -PathType Leaf)) { throw "RION_SIGNTOOL does not point at a file: $env:RION_SIGNTOOL" }
        return $env:RION_SIGNTOOL
    }
    $command = Get-Command 'signtool.exe' -ErrorAction SilentlyContinue
    if ($command) { return $command.Source }
    # Newest Windows SDK x64 signtool, when the SDK is installed but not on PATH.
    $candidate = Get-ChildItem -Path (Join-Path ${env:ProgramFiles(x86)} 'Windows Kits/10/bin') `
        -Filter 'signtool.exe' -Recurse -ErrorAction SilentlyContinue |
        Where-Object { $_.FullName -match '\\x64\\' } |
        Sort-Object FullName -Descending | Select-Object -First 1
    if ($candidate) { return $candidate.FullName }
    throw 'signtool.exe was not found. Install the Windows SDK signing tools or set RION_SIGNTOOL.'
}

function Assert-RionRuntimeConfig {
    param([Parameter(Mandatory)][string]$Path)

    $config = Get-Content -LiteralPath $Path -Raw | ConvertFrom-Json
    $frameworks = @($config.runtimeOptions.includedFrameworks)
    foreach ($name in @('Microsoft.NETCore.App', 'Microsoft.WindowsDesktop.App')) {
        $matches = @($frameworks | Where-Object { $_.name -eq $name })
        if ($matches.Count -ne 1) { throw "Missing or ambiguous bundled runtime: $name" }
        $version = [version]$matches[0].version
        if ($version.Major -ne 8 -or $version.Minor -ne 0 -or $version -lt [version]'8.0.31') {
            throw "Bundled $name $version does not meet the .NET 8.0.31 security servicing floor."
        }
    }
    return $frameworks
}

function Assert-RionDistPath {
    param([Parameter(Mandatory)][string]$Root, [Parameter(Mandatory)][string]$Path)

    $dist = [IO.Path]::GetFullPath((Join-Path $Root 'dist'))
    $target = [IO.Path]::GetFullPath($Path)
    if (!$target.StartsWith($dist + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) {
        throw "Release path must be below dist: $target"
    }
    # A lexical path check alone is insufficient if an existing parent is a junction.
    for ($current = $target; $current.Length -ge $dist.Length; $current = Split-Path $current -Parent) {
        if ((Test-Path -LiteralPath $current) -and
            ((Get-Item -LiteralPath $current -Force).Attributes -band [IO.FileAttributes]::ReparsePoint)) {
            throw "Release path cannot cross a link: $current"
        }
    }
}
