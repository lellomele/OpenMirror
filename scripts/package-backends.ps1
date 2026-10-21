param([string]$MsysRoot = 'C:\msys64', [string]$WorkRoot = '')
$ErrorActionPreference = 'Stop'
$appRoot = Split-Path -Parent $PSScriptRoot
if (!$WorkRoot) {
    $parent = Split-Path -Parent $appRoot
    if ((Split-Path -Leaf $parent) -eq 'outputs') { $WorkRoot = Join-Path (Split-Path -Parent $parent) 'work\backends' }
    else { $WorkRoot = Join-Path $appRoot '.build\backends' }
}
& (Join-Path $MsysRoot 'mingw64\bin\python.exe') (Join-Path $PSScriptRoot 'backend-tools.py') package --app $appRoot --work $WorkRoot --msys $MsysRoot
if ($LASTEXITCODE) { throw "Backend packaging failed ($LASTEXITCODE)." }
