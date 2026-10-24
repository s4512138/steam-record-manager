[CmdletBinding()]
param()
$ErrorActionPreference = 'Stop'
& (Join-Path $PSScriptRoot 'build.ps1')
& (Join-Path $PSScriptRoot 'build.ps1') -Tests
$repoRoot = Split-Path -Parent $PSScriptRoot
$fixtureRoot = Join-Path ([IO.Path]::GetTempPath()) ('srm-security-' + [Guid]::NewGuid().ToString('N'))
$target = Join-Path $fixtureRoot 'outside-target'
$junction = Join-Path $fixtureRoot 'bg_123_20200101_120000'
New-Item -ItemType Directory -Path $target -Force | Out-Null
[IO.File]::WriteAllText((Join-Path $target 'sentinel.txt'), 'do not modify')
New-Item -ItemType Junction -Path $junction -Value $target | Out-Null
$runner = Join-Path $repoRoot 'dist\SteamRecordManager.Tests.exe'
& $runner $junction (Join-Path $repoRoot 'dist\SteamRecordManager.exe')
if ($LASTEXITCODE -ne 0) { throw "Tests failed with exit code $LASTEXITCODE." }
# Leave synthetic fixtures for inspection. Never recursively remove a directory containing a junction.
