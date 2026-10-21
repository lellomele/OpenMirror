param(
    [string]$MsysRoot = 'C:\msys64',
    [string]$WorkRoot = '',
    [switch]$SkipSources
)
$ErrorActionPreference = 'Stop'
$appRoot = Split-Path -Parent $PSScriptRoot
if (!$WorkRoot) {
    $parent = Split-Path -Parent $appRoot
    if ((Split-Path -Leaf $parent) -eq 'outputs') { $WorkRoot = Join-Path (Split-Path -Parent $parent) 'work\backends' }
    else { $WorkRoot = Join-Path $appRoot '.build\backends' }
}
$python = Join-Path $MsysRoot 'mingw64\bin\python.exe'
if (!(Test-Path -LiteralPath $python)) { throw 'Install the MSYS2 MINGW64 Python, GCC, CMake, Ninja, pkgconf and binutils tools first.' }
$arguments = @((Join-Path $PSScriptRoot 'backend-tools.py'), 'build', '--app', $appRoot, '--work', $WorkRoot, '--msys', $MsysRoot)
if ($SkipSources) { $arguments += '--skip-sources' }
& $python @arguments
if ($LASTEXITCODE) { throw "Backend build failed ($LASTEXITCODE)." }
