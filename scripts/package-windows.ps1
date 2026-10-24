[CmdletBinding()]
param()
$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $PSScriptRoot
$binary = Join-Path $repoRoot 'dist\SteamRecordManager.exe'
if (-not (Test-Path -LiteralPath $binary)) { throw 'Build and test the application first.' }
$stage = Join-Path ([IO.Path]::GetTempPath()) ('srm-release-windows-' + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $stage -Force | Out-Null
Copy-Item -LiteralPath $binary -Destination $stage
foreach ($file in @('README.md','README.zh-TW.md','LICENSE','SECURITY.md','THIRD_PARTY_NOTICES.md','CONTRIBUTING.md','CHANGELOG.md')) { Copy-Item -LiteralPath (Join-Path $repoRoot $file) -Destination $stage }
Copy-Item -LiteralPath (Join-Path $repoRoot 'docs') -Destination (Join-Path $stage 'docs') -Recurse
$releaseRoot = Join-Path $repoRoot 'artifacts\release'
New-Item -ItemType Directory -Path $releaseRoot -Force | Out-Null
$archive = Join-Path $releaseRoot 'SteamRecordManager-windows-x64.zip'
Compress-Archive -Path (Join-Path $stage '*') -DestinationPath $archive -Force
Write-Output $archive
