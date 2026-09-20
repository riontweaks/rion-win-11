param([Parameter(Mandatory)][string]$CompilerPath, [string]$PackageDirectory='dist/free', [string]$OutputDirectory='dist/free-installer')
$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
. (Join-Path $PSScriptRoot 'ReleaseSupport.ps1')
$source=Get-RionSourceManifest -Root $root
$package=Join-Path $root $PackageDirectory
Assert-RionDistPath -Root $root -Path $package
$projectPath=Join-Path $root 'src/RionSuite/src/RionHub/RionHub.csproj'
$project=[xml](Get-Content -LiteralPath $projectPath -Raw)
$iconEntries=@($project.SelectNodes('/Project/PropertyGroup/ApplicationIcon'))
if($iconEntries.Count -ne 1 -or [string]::IsNullOrWhiteSpace($iconEntries[0].InnerText)){throw 'Expected exactly one ApplicationIcon in the application project.'}
$applicationIcon=(Resolve-Path -LiteralPath (Join-Path (Split-Path $projectPath -Parent) $iconEntries[0].InnerText)).Path
if([IO.Path]::GetExtension($applicationIcon) -ne '.ico'){throw 'ApplicationIcon must be an ICO file.'}
$compressionDictionary=64
$manifest=Get-Content -LiteralPath (Join-Path $package 'release-manifest.json') -Raw | ConvertFrom-Json
if($manifest.source.sha256 -ne $source.sha256){throw 'Published source does not match current source. Run Publish.ps1 first.'}
foreach($file in $manifest.artifacts){
    $path=[IO.Path]::GetFullPath((Join-Path $package $file.path))
    if(!$path.StartsWith($package+[IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase)){throw 'Invalid package manifest path.'}
    if((Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash -ne $file.sha256){throw "Package was changed: $($file.path)"}
}
$compiler=(Resolve-Path -LiteralPath $CompilerPath).Path
$version=& $compiler /VERSION
if($LASTEXITCODE -ne 0){throw 'NSIS compiler version check failed.'}
if("$version".Trim() -ne 'v3.12'){throw "Expected NSIS 3.12; got $version"}
$stage=Join-Path $root ('dist/.staging/installer-'+(Get-Date -Format yyyyMMdd-HHmmss)+'-'+[Guid]::NewGuid().ToString('N').Substring(0,8))
Assert-RionDistPath -Root $root -Path $stage
New-Item -ItemType Directory -Path $stage -Force|Out-Null
function Quote-Nsis([string]$text){'"'+$text.Replace('$','$$').Replace('"','$\"')+'"'}
$files=@(Get-ChildItem -LiteralPath $package -File -Recurse | Sort-Object FullName)
$install=[Collections.Generic.List[string]]::new();$remove=[Collections.Generic.List[string]]::new();$directories=[Collections.Generic.HashSet[string]]::new()
foreach($file in $files){
    $relative=$file.FullName.Substring($package.Length+1)
    if($relative -match '[\r\n]'){throw 'Invalid package filename.'}
    $sub=Split-Path $relative -Parent
    $destination='$INSTDIR'+$(if($sub){'\'+$sub}else{''})
    $install.Add('SetOutPath "'+$destination.Replace('"','$\"')+'"')
    $install.Add('ClearErrors')
    $install.Add('File '+(Quote-Nsis $file.FullName))
    $install.Add('IfErrors 0 +3')
    $install.Add('MessageBox MB_ICONSTOP "A required file could not be installed. Close Rion and run setup again."')
    $install.Add('Abort')
    $remove.Add('Delete "$INSTDIR\'+$relative.Replace('$','$$').Replace('"','$\"')+'"')
    if($sub){[void]$directories.Add($sub)}
}
foreach($directory in $directories | Sort-Object Length -Descending){$remove.Add('RMDir "$INSTDIR\'+$directory+'"')}
$installFile=Join-Path $stage 'install-files.nsh';$removeFile=Join-Path $stage 'remove-files.nsh'
$install | Set-Content -LiteralPath $installFile -Encoding UTF8
$remove | Set-Content -LiteralPath $removeFile -Encoding UTF8
$output=Join-Path $stage 'Rion-Win11-Setup.exe'
$editionOptions = @()
if ($manifest.edition -eq 'Free') { $editionOptions += '/DFREE_EDITION' }
& $compiler /V2 @editionOptions "/DPACKAGE=$package" "/DOUTPUT=$output" "/DAPP_ICON=$applicationIcon" "/DCOMPRESSION_DICTIONARY=$compressionDictionary" "/DINSTALL_FILES=$installFile" "/DREMOVE_FILES=$removeFile" (Join-Path $PSScriptRoot 'installer/Rion.nsi')
if($LASTEXITCODE -ne 0){throw "Installer compile failed: $LASTEXITCODE"}
if(!(Test-Path -LiteralPath $output -PathType Leaf)){throw 'Installer output missing.'}
if((Get-RionSourceManifest -Root $root).sha256 -ne $source.sha256){throw 'Source changed during installer compilation.'}
$signing=Invoke-RionSignFile -Path $output
$hash=(Get-FileHash -LiteralPath $output -Algorithm SHA256).Hash
$installerOptions=[ordered]@{iconSource=$applicationIcon.Substring($root.Length+1).Replace('\','/');iconSha256=(Get-FileHash -LiteralPath $applicationIcon).Hash;compression='solid-lzma';dictionaryMiB=$compressionDictionary;installerBytes=(Get-Item -LiteralPath $output).Length}
[ordered]@{createdUtc=[DateTime]::UtcNow.ToString('o');sourceHash=$source.sha256;compilerVersion="$version".Trim();compilerSha256=(Get-FileHash -LiteralPath $compiler).Hash;installerSha256=$hash;options=$installerOptions;signing=$signing;validation='Compiled and package hashes checked; install/upgrade/uninstall have not been executed on a clean VM.';coreRuntime='Included in self-contained application';optionalUtilities='Not embedded in installer; downloaded separately by application';files=$manifest.artifacts} | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath (Join-Path $stage 'installer-manifest.json') -Encoding UTF8
$destination=Join-Path $root $OutputDirectory;Assert-RionDistPath -Root $root -Path $destination
if(Test-Path -LiteralPath $destination){$previous=Join-Path $root ('dist/previous/installer-'+[Guid]::NewGuid().ToString('N'));Assert-RionDistPath -Root $root -Path $previous;New-Item -ItemType Directory -Path (Split-Path $previous -Parent) -Force|Out-Null;Move-Item -LiteralPath $destination -Destination $previous}
New-Item -ItemType Directory -Path $destination|Out-Null
Copy-Item -LiteralPath $output,(Join-Path $stage 'installer-manifest.json') -Destination $destination
$hash+'  Rion-Win11-Setup.exe' | Set-Content -LiteralPath (Join-Path $destination 'SHA256SUMS.txt') -Encoding ascii
Get-Item -LiteralPath (Join-Path $destination 'Rion-Win11-Setup.exe') | Select-Object FullName,Length,LastWriteTimeUtc
Write-Output "SHA256 $hash"
