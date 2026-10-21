param([switch]$Test,[string]$OutputFile='',[switch]$ExperimentalProtocols)
$ErrorActionPreference='Stop'
$root=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
if(-not $OutputFile){$OutputFile=Join-Path $root 'OpenMirror.exe'}
$OutputFile=[IO.Path]::GetFullPath($OutputFile)
New-Item -ItemType Directory -Force (Split-Path -Parent $OutputFile) | Out-Null
$compiler=Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
if(-not (Test-Path -LiteralPath $compiler)){throw 'Compilatore .NET Framework 4.x non trovato. Usa Windows 10/11 x64 con .NET Framework 4.8.'}
if(-not (Test-Path (Join-Path $root 'assets\openmirror.ico'))){& (Join-Path $PSScriptRoot 'generate-icon.ps1')}
$wpf=Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\WPF'
$arguments=@('/nologo','/target:winexe','/platform:x64','/optimize+','/warn:4',('/out:'+$OutputFile),('/win32icon:'+(Join-Path $root 'assets\openmirror.ico')),('/win32manifest:'+(Join-Path $root 'app.manifest')),'/reference:System.dll','/reference:System.Core.dll','/reference:System.Drawing.dll','/reference:System.Windows.Forms.dll','/reference:System.Runtime.Serialization.dll','/reference:System.Xaml.dll')
if($ExperimentalProtocols){$arguments+='/define:EXPERIMENTAL_PROTOCOLS'}
foreach($library in 'WindowsBase.dll','PresentationCore.dll','PresentationFramework.dll','WindowsFormsIntegration.dll'){$arguments+=('/reference:'+(Join-Path $wpf $library))}
$sources=Get-ChildItem -LiteralPath (Join-Path $root 'src') -Filter '*.cs' | Select-Object -ExpandProperty FullName
& $compiler @arguments @sources
if($LASTEXITCODE -ne 0){throw 'Compilazione non riuscita.'}
$config=Join-Path $root 'OpenMirror.exe.config'
if($config -ne ($OutputFile+'.config')){Copy-Item -LiteralPath $config -Destination ($OutputFile+'.config') -Force}
Write-Host "Creato: $OutputFile"
if($Test){
 $testDir=Join-Path $root '..\..\work\tests'
 New-Item -ItemType Directory -Force $testDir | Out-Null
 $testExe=Join-Path $testDir 'CaptureTests.exe'
 & $compiler /nologo /target:exe ('/out:'+$testExe) /reference:System.dll /reference:System.Core.dll /reference:System.Drawing.dll /reference:System.Windows.Forms.dll /reference:System.Runtime.Serialization.dll (Join-Path $root 'src\Capture.cs') (Join-Path $root 'src\Core.cs') (Join-Path $root 'tests\TestLoc.cs') (Join-Path $root 'tests\CaptureTests.cs')
 if($LASTEXITCODE -ne 0){throw 'Compilazione test non riuscita.'}
 & $testExe
 if($LASTEXITCODE -ne 0){throw 'Test acquisizione non superati.'}
 $coreExe=Join-Path $testDir 'CoreTests.exe'
 & $compiler /nologo /target:exe /platform:x64 ('/out:'+$coreExe) /reference:System.dll /reference:System.Core.dll /reference:System.Drawing.dll /reference:System.Windows.Forms.dll /reference:System.Runtime.Serialization.dll (Join-Path $root 'src\Capture.cs') (Join-Path $root 'src\Core.cs') (Join-Path $root 'tests\TestLoc.cs') (Join-Path $root 'tests\CoreTests.cs')
 if($LASTEXITCODE -ne 0){throw 'Compilazione test ricevitore non riuscita.'}
 & $coreExe
 if($LASTEXITCODE -ne 0){throw 'Test ricevitore non superati.'}
}
