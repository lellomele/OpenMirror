# Renders and checks the WPF layout without showing any desktop windows.
param([string]$OutputFolder='')
$ErrorActionPreference='Stop'
$root=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$testRoot=Join-Path $root '.build\layout-test'
New-Item -ItemType Directory -Force $testRoot | Out-Null
if(-not $OutputFolder){$OutputFolder=Join-Path $testRoot 'rendered'}
$executable=Join-Path $testRoot 'OpenMirror.exe'
& (Join-Path $PSScriptRoot 'build.ps1') -OutputFile $executable
if(-not (Test-Path -LiteralPath $executable)){throw 'Layout build missing.'}
$process=New-Object Diagnostics.Process
$process.StartInfo=New-Object Diagnostics.ProcessStartInfo
$process.StartInfo.FileName=$executable
$process.StartInfo.Arguments='--verify-layout "'+[IO.Path]::GetFullPath($OutputFolder)+'"'
$process.StartInfo.UseShellExecute=$false
$process.StartInfo.CreateNoWindow=$true
[void]$process.Start()
if(-not $process.WaitForExit(30000)){$process.Kill();throw 'Layout verification timed out.'}
if($process.ExitCode -ne 0){Get-Content (Join-Path $OutputFolder 'layout-report.txt') -ErrorAction SilentlyContinue;throw 'Layout verification failed.'}
Get-Content (Join-Path $OutputFolder 'layout-report.txt')
