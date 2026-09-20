$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
. (Join-Path $PSScriptRoot 'ReleaseSupport.ps1')
$output=Join-Path $root 'dist'
New-Item -ItemType Directory -Path $output -Force | Out-Null
$zipPath=Join-Path $output ('Rion-Win-11-source-'+(Get-Date -Format 'yyyyMMdd-HHmmss')+'.zip')
$files=@(Get-RionSourceFiles -Root $root)
Add-Type -AssemblyName System.IO.Compression
Add-Type -AssemblyName System.IO.Compression.FileSystem
$archive=[IO.Compression.ZipFile]::Open($zipPath,[IO.Compression.ZipArchiveMode]::Create)
try{
    foreach($file in $files){
        $entry=$file.Substring($root.Length+1).Replace('\','/')
        [IO.Compression.ZipFileExtensions]::CreateEntryFromFile($archive,$file,'Rion Win 11/'+$entry,[IO.Compression.CompressionLevel]::Optimal) | Out-Null
    }
}finally{$archive.Dispose()}
(Get-FileHash -LiteralPath $zipPath -Algorithm SHA256).Hash+'  '+(Split-Path $zipPath -Leaf) | Set-Content ($zipPath+'.sha256') -Encoding ascii
"Exported $($files.Count) source/documentation files to $zipPath"
