param([string]$Destination='', [string]$Executable='OpenMirror.exe',[string]$Version='0.2',[ValidateSet('AirPlay','Experimental')][string]$Edition='AirPlay',[switch]$ReplaceExisting)
$ErrorActionPreference='Stop'
Add-Type -AssemblyName System.IO.Compression
Add-Type -AssemblyName System.IO.Compression.FileSystem
$root=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
if($Version -notmatch '^\d+\.\d+(\.\d+)?$'){throw 'Versione non valida.'}
if([IO.Path]::GetFileName($Executable) -ne $Executable -or $Executable -notlike '*.exe'){throw 'Specificare il nome dell''eseguibile nella cartella principale.'}
if(-not $Destination){$Destination=Split-Path -Parent $root}
$Destination=[IO.Path]::GetFullPath($Destination)
New-Item -ItemType Directory -Force $Destination | Out-Null
if(-not (Test-Path -LiteralPath (Join-Path $root $Executable))){throw 'Compila prima l''eseguibile.'}
$all=Get-ChildItem -LiteralPath $root -Recurse -File | Where-Object { $_.FullName -notmatch '[\\/](\.git|\.build|preview|__pycache__)[\\/]' }
if($all | Where-Object { $_.Extension -in '.pfx','.p12','.key','.dpapi' }){throw 'Rimuovere le chiavi private e i PFX dalla cartella di distribuzione.'}
if($all | Where-Object { $_.Extension -eq '.pem' } | Select-String -Pattern 'PRIVATE KEY' -SimpleMatch){throw 'Un file PEM contiene una chiave privata: escluderlo dalla distribuzione.'}
function Create-ReleaseZip([string]$Name,[bool]$Source){
 $target=Join-Path $Destination $Name
 if((Test-Path -LiteralPath $target) -and -not $ReplaceExisting){throw "Archivio già esistente: $target"}
 $stream=[IO.File]::Open($target,$(if($ReplaceExisting){[IO.FileMode]::Create}else{[IO.FileMode]::CreateNew}))
 $zip=New-Object IO.Compression.ZipArchive($stream,[IO.Compression.ZipArchiveMode]::Create,$false)
 try{
  foreach($file in $all){
   $relative=$file.FullName.Substring($root.Length+1).Replace('\','/')
   if($Edition -eq 'AirPlay' -and ($relative -match '^(bin/(android|cast|miracast)/|companion/|backend/ChromeCastReceiver/|tests/(ChromeCast|CHROME-CAST|miracast-))' -or $relative -in 'docs/GOOGLE-CAST.md','docs/ANALISI-REA.md')){continue}
   $entry=$relative
   $isRoot=$relative.IndexOf('/') -lt 0
   if($Source){
    if($relative.StartsWith('bin/') -or ($isRoot -and $relative.EndsWith('.exe'))){continue}
    if($isRoot -and $relative.EndsWith('.exe.config') -and $relative -ne 'OpenMirror.exe.config'){continue}
   }else{
    if($relative -eq $Executable){$entry='OpenMirror.exe'}
    elseif($relative -eq ($Executable+'.config')){$entry='OpenMirror.exe.config'}
    elseif($isRoot -and ($relative.EndsWith('.exe') -or $relative.EndsWith('.exe.config'))){continue}
    elseif($relative.StartsWith('src/') -or $relative.StartsWith('tests/') -or $relative.StartsWith('scripts/') -or $relative.StartsWith('backend/') -or $relative.StartsWith('companion/') -or $relative.StartsWith('third_party/msys2-sources/') -or $relative.StartsWith('third_party/scrcpy-dependencies/') -or $relative.StartsWith('third_party/UxPlay/') -or $relative.EndsWith('.tar.gz') -or $relative.EndsWith('.gitignore')){continue}
   }
   $compression=if($file.Extension -in '.zst','.gz','.xz','.zip','.appx','.msix'){[IO.Compression.CompressionLevel]::NoCompression}else{[IO.Compression.CompressionLevel]::Optimal}
   [IO.Compression.ZipFileExtensions]::CreateEntryFromFile($zip,$file.FullName,('OpenMirror/'+$entry),$compression) | Out-Null
  }
 }finally{$zip.Dispose();$stream.Dispose()}
 Write-Host "Creato $target"
}
$release='OpenMirror-'+$Version+$(if($Edition -eq 'AirPlay'){'-AirPlay'}else{'-Experimental'})
Create-ReleaseZip ($release+'-windows-x64.zip') $false
Create-ReleaseZip ($release+'-source.zip') $true
$lines=Get-ChildItem -LiteralPath $Destination -Filter ($release+'-*.zip') | ForEach-Object { $hash=Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256; $hash.Hash.ToLowerInvariant()+'  '+$_.Name }
[IO.File]::WriteAllLines((Join-Path $Destination ($release+'-SHA256SUMS.txt')),[string[]]$lines)
