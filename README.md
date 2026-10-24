# Steam Record Manager

[繁體中文](README.zh-TW.md) · English

A small, local Windows application for inspecting and managing the disk space used by Steam Game Recording. Independent community software; not affiliated with Valve.

**Version 1.3.1 — index editing remains beta.** File-level regression tests pass, but Steam reloading an edited index has not been validated end to end. Index synchronization is off by default. Fully exit Steam before deleting recordings or changing indexes.

## Features

- Traditional Chinese, Simplified Chinese and English, with a remembered language preference.
- Recording sessions grouped by folder, game name, modification time and logical size.
- Offline game-name lookup using installed-game manifests and the local Steam appinfo cache (v39–v41).
- Search, protection flags and cleanup suggestions based on age or a background-recording size limit.
- Complete-session deletion, using Windows Recycle Bin dialogs or explicitly confirmed permanent deletion. Clips and manual recordings are excluded from automatic suggestions.
- CSV exports with formula-like cells escaped as text.
- Optional index synchronization and a separate preview of confirmed missing local references. All timeline objects, events and timeline JSON files are retained.

## Requirements

64-bit Windows 10/11 with .NET Framework 4.8 and an interactive desktop session. No Python, Steam API key, administrator privileges or third-party packages are required. Recording roots and reports must use local drive paths, not UNC/device paths or junctions.

## Build and run

Open PowerShell in the repository root:

```powershell
.\scripts\build.ps1
.\dist\SteamRecordManager.exe
```

The build uses the C# compiler and assemblies already installed with the Windows .NET Framework. It does not download dependencies. The executable requests the current user's permissions (`asInvoker`) and is not code-signed. An unsigned build is not a security certification; do not disable security controls to run it.

1. Select a Steam recording root containing `video` and `clips`.
2. Scan, review the sessions and protect important items.
3. Set the size limit and retention age, then preview the suggestions. `0` disables that rule. Suggestions affect the current search results; they never execute automatically. Protected/recent recordings may prevent meeting the limit.
4. Fully exit Steam. Review the final list before moving sessions to the Recycle Bin or typing `DELETE` to permanently remove them. For the Recycle Bin route, type `RECYCLE`.

Windows may offer a permanent-deletion prompt if recycling is unavailable; cancel that prompt if you require recoverability. Moving files to the same drive's Recycle Bin does not free disk space until the bin is emptied. Logical GiB totals may differ from physical space reclaimed because of hard links, compression and allocation.

## Index editing: read before enabling

`gamerecording.pb` is an internal Steam format, not a stable public editing API. See [index behavior and recovery](docs/INDEX_FORMAT.md) before using the beta functions:

- **Sync index cleanup (beta)** removes references for successfully deleted local recording folders. A preflight rejects selected recordings containing remote-reference fields.
- **Repair missing references** previews eligible local references whose source folders no longer exist. It does not delete videos. Type `REPAIR` to apply the preview.
- Every index write has a full pre-change backup, a temporary-file write and replacement, and content checks before and after writing.
- Timeline objects, achievements, phases, post-game events and timeline JSON remain. An empty timeline is not treated as garbage. Unknown fields in retained messages keep their original bytes.
- A batch is not a transaction. A video may be deleted before an index update fails. Review the operation log, then preview repair rather than assuming rollback occurred.
- Restoring a folder from the Recycle Bin does not restore its index. Do not overwrite a newer index with an old backup after new recordings have been created.

## Read-only command line

Use a **new report filename outside the recording root**. Existing output files are refused on the command line.

```powershell
.\dist\SteamRecordManager.exe --scan 'D:\Recordings' "$env:TEMP\recordings-new.csv"
.\dist\SteamRecordManager.exe --index-audit 'D:\Recordings' "$env:TEMP\index-audit-new.txt"
```

There is no deletion, index-write or test-bypass command in the release CLI. Tests use a separately compiled console executable.

## Privacy and local files

The application has no HTTP client, telemetry, account sign-in, uploader, update downloader, injected code or browser-debug connection. It reads local Steam metadata to resolve names and may inspect the Steam process path. It does not request access to a Steam account.

Preferences, protected folder paths and operation history are stored under `%LOCALAPPDATA%\SteamRecordManager`. Reports and logs may reveal game names, dates, absolute paths and recording/index metadata. Keep these private; do not attach unredacted reports or real `.pb` files to public issues. [Privacy details](docs/PRIVACY.md).

## Tests and development

```powershell
.\scripts\test.ps1
```

This builds release and test binaries, then runs recording, language, appinfo, index and security regressions on synthetic fixtures. A junction fixture verifies link refusal. Fixtures are retained in a fresh temporary directory for inspection. Real Steam recordings are not deleted or rewritten by tests. [Development guide](CONTRIBUTING.md).

GitHub Actions is configured to run the same build and tests on Windows. Its remote result is not claimed until you push the repository and it runs.

## Security status and known limits

The source has received a local review and targeted regression tests. This is not an independent penetration test or a guarantee of zero vulnerabilities. In particular, path checks and Steam-process checks do not provide complete protection against a malicious same-user process racing filesystem changes. The tool is intended for a trusted local account and is not an administrative cleanup service. Do not run it elevated. [Review scope and findings](docs/SECURITY_REVIEW.md) · [Report a vulnerability](SECURITY.md).

## Project layout

```text
src/        Windows Forms application, metadata parsers and guarded exports
tests/      Synthetic regression/security tests; excluded from release builds
scripts/    Offline build and test entry points
docs/       Index behavior, privacy, review and publishing notes
.github/    Windows CI, issue forms and pull-request template
```

## License and references

MIT — see [LICENSE](LICENSE). Steam trademarks belong to Valve. Format references and the distinction between references and shipped dependencies are documented in [THIRD_PARTY_NOTICES.md](THIRD_PARTY_NOTICES.md).
