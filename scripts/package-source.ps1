[CmdletBinding()]
param()
$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $PSScriptRoot
$outputRoot = Join-Path $repoRoot 'artifacts'
New-Item -ItemType Directory -Path $outputRoot -Force | Out-Null
$archivePath = Join-Path $outputRoot 'SteamRecordManager-source-1.4.0-preview.1.zip'
if (Test-Path -LiteralPath $archivePath) { throw 'Source archive already exists; move it elsewhere before creating another.' }
$topLevel = @('README.md','README.zh-TW.md','LICENSE','SECURITY.md','CONTRIBUTING.md','CHANGELOG.md','THIRD_PARTY_NOTICES.md','.gitignore','.gitattributes','.editorconfig')
$rules = @{src=@('.cs','.manifest');tests=@('.cs');macos=@('.swift','.plist');scripts=@('.ps1','.sh');docs=@('.md');'.github'=@('.yml','.md')}
$files = @($topLevel | ForEach-Object { Get-Item -LiteralPath (Join-Path $repoRoot $_) -Force })
foreach ($folder in $rules.Keys) {
    $items = @(Get-ChildItem -LiteralPath (Join-Path $repoRoot $folder) -Recurse -Force)
    if ($items | Where-Object { ($_.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0 }) { throw "Linked content refused in $folder." }
    $files += @($items | Where-Object { -not $_.PSIsContainer -and $_.Extension -in $rules[$folder] })
}
Add-Type -AssemblyName System.IO.Compression
$archiveStream = [IO.FileStream]::new($archivePath, [IO.FileMode]::CreateNew)
try {
    $zip = [IO.Compression.ZipArchive]::new($archiveStream, [IO.Compression.ZipArchiveMode]::Create, $true)
    try {
        foreach ($file in ($files | Sort-Object FullName)) {
            if (($file.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0) { throw 'Linked source file refused.' }
            $relative = $file.FullName.Substring($repoRoot.Length + 1).Replace('\','/')
            $entry = $zip.CreateEntry('SteamRecordManager/' + $relative)
            $entryStream = $entry.Open()
            try { $inputStream = [IO.File]::OpenRead($file.FullName); try { $inputStream.CopyTo($entryStream) } finally { $inputStream.Dispose() } } finally { $entryStream.Dispose() }
        }
    } finally { $zip.Dispose() }
} finally { $archiveStream.Dispose() }
Write-Output $archivePath
