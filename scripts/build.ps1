[CmdletBinding()]
param([switch]$Tests)
$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $PSScriptRoot
$framework = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319'
$compiler = Join-Path $framework 'csc.exe'
if (-not (Test-Path -LiteralPath $compiler)) { throw 'Windows with the .NET Framework C# compiler is required.' }
$vb = Join-Path $framework 'Microsoft.VisualBasic.dll'
if (-not (Test-Path -LiteralPath $vb)) {
    $assemblyRoot = Join-Path $env:WINDIR 'Microsoft.NET\assembly\GAC_MSIL\Microsoft.VisualBasic'
    $vb = (Get-ChildItem -LiteralPath $assemblyRoot -Filter Microsoft.VisualBasic.dll -Recurse | Select-Object -First 1).FullName
}
if (-not $vb) { throw 'Microsoft.VisualBasic.dll was not found in the Windows framework.' }
$destination = Join-Path $repoRoot 'dist'
New-Item -ItemType Directory -Path $destination -Force | Out-Null
$binary = Join-Path $destination $(if ($Tests) { 'SteamRecordManager.Tests.exe' } else { 'SteamRecordManager.exe' })
$sources = @(Get-ChildItem -LiteralPath (Join-Path $repoRoot 'src') -Filter '*.cs' | ForEach-Object FullName)
$arguments = @('/nologo','/optimize+','/utf8output','/warn:4','/warnaserror+',('/out:' + $binary),'/reference:System.Windows.Forms.dll','/reference:System.Drawing.dll',('/reference:' + $vb))
if ($Tests) {
    $sources += @(Get-ChildItem -LiteralPath (Join-Path $repoRoot 'tests') -Filter '*.cs' | ForEach-Object FullName)
    $arguments += @('/target:exe','/define:TESTS','/main:SteamRecordManager.TestRunner')
} else {
    $arguments += @('/target:winexe',('/win32manifest:' + (Join-Path $repoRoot 'src\app.manifest')))
}
& $compiler @arguments @sources
if ($LASTEXITCODE -ne 0) { throw "Compilation failed with exit code $LASTEXITCODE." }
Write-Output $binary
