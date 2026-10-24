# Steam Record Manager

[繁體中文](README.zh-TW.md) · English

*Code generated with GPT-6.1 Sol*

A small, local desktop application for inspecting and managing the disk space used by Steam Game Recording. Independent community software; not affiliated with Valve.

**1.4.0 Preview — Windows and a new native macOS preview.** Index editing is Windows-only and remains beta; Steam reloading an edited index has not been validated end to end. Fully exit Steam before cleanup on either platform.

## Installation

| Computer | Release asset | After extracting |
| --- | --- | --- |
| Windows 10/11, 64-bit | [Windows app](https://github.com/s4512138/steam-record-manager/releases/download/v1.4.0-preview.1/SteamRecordManager-windows-x64.zip) | Open `SteamRecordManager.exe` |
| macOS 13+, Apple Silicon (M1/M2/M3/…) | [macOS arm64 preview](https://github.com/s4512138/steam-record-manager/releases/download/v1.4.0-preview.1/SteamRecordManager-macos-arm64-preview.zip) | Open `Steam Record Manager.app`; optionally move it to Applications |
| macOS 13+, Intel | [macOS Intel preview](https://github.com/s4512138/steam-record-manager/releases/download/v1.4.0-preview.1/SteamRecordManager-macos-x64-preview.zip) | Open `Steam Record Manager.app`; optionally move it to Applications |

Windows requires .NET Framework 4.8, normally present on current Windows installations. The native macOS app uses system frameworks; no Python, .NET, Xcode or developer tools are needed on the user's Mac. `SHA256SUMS.txt` contains checksums for the three release archives.

**Signing:** Windows builds are unsigned. macOS builds are ad-hoc signed for integrity checks, **not** Developer ID signed or Apple-notarized. macOS may block downloaded builds; this preview does not promise a warning-free launch. Do not disable Gatekeeper or other system protections. [macOS preview scope and signing](docs/MACOS_PREVIEW.md).

## Platform differences

| Capability | Windows | macOS preview |
| --- | --- | --- |
| Three languages, local names, scan/filter, protection and cleanup suggestions | Yes | Yes |
| Complete-session cleanup | Recycle Bin or confirmed permanent deletion | Native Trash only; no permanent-delete function |
| Index synchronization/repair | Opt-in beta | Not implemented; indexes remain unchanged |
| CSV reports and read-only report CLI | Yes | Not implemented |

The macOS app is a new implementation, not a repackaged Windows executable. It is not yet feature-equivalent. It retains Steam indexes and may leave stale Steam library entries after a session is moved to Trash.

## Windows features

- Traditional Chinese, Simplified Chinese and English, with a remembered language preference.
- Recording sessions grouped by folder, game name, modification time and logical size.
- Offline game-name lookup using installed-game manifests and the local Steam appinfo cache (v39–v41).
- Search, protection flags and cleanup suggestions based on age or a background-recording size limit.
- Complete-session deletion, using Windows Recycle Bin dialogs or explicitly confirmed permanent deletion. Clips and manual recordings are excluded from automatic suggestions.
- CSV exports with formula-like cells escaped as text.
- Optional index synchronization and a separate preview of confirmed missing local references. All timeline objects, events and timeline JSON files are retained.

## Developer requirements

Windows source builds require 64-bit Windows 10/11 with .NET Framework 4.8. macOS source builds require macOS 13+ and Xcode Command Line Tools. Both need an interactive desktop for GUI use. End users should use the release downloads above. No Steam API key or administrator privileges are required. Linked recording roots are rejected on both platforms; Windows also rejects UNC/device recording/report paths.

## Build from source

Open PowerShell in the repository root:

```powershell
.\scripts\build.ps1
.\dist\SteamRecordManager.exe
```

The build uses the C# compiler and assemblies already installed with the Windows .NET Framework. It does not download dependencies. The executable requests the current user's permissions (`asInvoker`) and is not code-signed. An unsigned build is not a security certification; do not disable security controls to run it.

On a Mac, build the native app for the build machine's architecture:

```sh
bash scripts/build-macos.sh
open 'dist/Steam Record Manager.app'
```

That script also runs synthetic core tests, a native-window initialization smoke test and ad-hoc signature verification. It does not notarize the app. The release workflow builds Apple Silicon and Intel separately on native Mac runners.

1. Select a Steam recording root containing `video` and `clips`.
2. Scan, review the sessions and protect important items.
3. Set the size limit and retention age, then preview the suggestions. `0` disables that rule. Suggestions affect the current search results; they never execute automatically. Protected/recent recordings may prevent meeting the limit.
4. Fully exit Steam. Review the final list before moving sessions to the Recycle Bin or typing `DELETE` to permanently remove them. For the Recycle Bin route, type `RECYCLE`.

Windows may offer a permanent-deletion prompt if recycling is unavailable; cancel that prompt if you require recoverability. Moving files to the same drive's Recycle Bin does not free disk space until the bin is emptied. Logical GiB totals may differ from physical space reclaimed because of hard links, compression and allocation.

## Windows index editing: read before enabling

`gamerecording.pb` is an internal Steam format, not a stable public editing API. See [index behavior and recovery](docs/INDEX_FORMAT.md) before using the beta functions:

- **Sync index cleanup (beta)** removes references for successfully deleted local recording folders. A preflight rejects selected recordings containing remote-reference fields.
- **Repair missing references** previews eligible local references whose source folders no longer exist. It does not delete videos. Type `REPAIR` to apply the preview.
- Every index write has a full pre-change backup, a temporary-file write and replacement, and content checks before and after writing.
- Timeline objects, achievements, phases, post-game events and timeline JSON remain. An empty timeline is not treated as garbage. Unknown fields in retained messages keep their original bytes.
- A batch is not a transaction. A video may be deleted before an index update fails. Review the operation log, then preview repair rather than assuming rollback occurred.
- Restoring a folder from the Recycle Bin does not restore its index. Do not overwrite a newer index with an old backup after new recordings have been created.

## Windows read-only command line

Use a **new report filename outside the recording root**. Existing output files are refused on the command line.

```powershell
.\dist\SteamRecordManager.exe --scan 'D:\Recordings' "$env:TEMP\recordings-new.csv"
.\dist\SteamRecordManager.exe --index-audit 'D:\Recordings' "$env:TEMP\index-audit-new.txt"
```

There is no deletion, index-write or test-bypass command in the release CLI. Tests use a separately compiled console executable.

## Privacy and local files

The application has no HTTP client, telemetry, account sign-in, uploader, update downloader, injected code or browser-debug connection. It reads local Steam metadata to resolve names and may inspect the Steam process path. It does not request access to a Steam account.

Windows preferences, protected folder paths and operation history are stored under `%LOCALAPPDATA%\SteamRecordManager`. macOS language/protection preferences use the app's `UserDefaults` domain, `org.steamrecordmanager.preview`. Reports and logs may reveal game names, dates, absolute paths and recording/index metadata. Keep these private; do not attach unredacted reports or real `.pb` files to public issues. [Privacy details](docs/PRIVACY.md).

## Tests and development

```powershell
.\scripts\test.ps1
```

This builds release and test binaries, then runs recording, language, appinfo, index and security regressions on synthetic fixtures. A junction fixture verifies link refusal. Fixtures are retained in a fresh temporary directory for inspection. Real Steam recordings are not deleted or rewritten by tests. [Development guide](CONTRIBUTING.md).

GitHub Actions is configured to run the same build and tests on Windows. Its remote result is not claimed until you push the repository and it runs.

`Build desktop release` is manually dispatched. All Windows, Apple Silicon and Intel builds must pass before its explicitly selected `publish` job can create the prerelease. Publishing is restricted to `main`; it uploads only the three application archives and checksums. The write-scoped GitHub token is limited to that publishing job. No signing credential is bundled or created by the workflow.

## Security status and known limits

The source has received a local review and targeted regression tests. This is not an independent penetration test or a guarantee of zero vulnerabilities. In particular, path checks and Steam-process checks do not provide complete protection against a malicious same-user process racing filesystem changes. The tool is intended for a trusted local account and is not an administrative cleanup service. Do not run it elevated. [Review scope and findings](docs/SECURITY_REVIEW.md) · [Report a vulnerability](SECURITY.md).

## Project layout

```text
src/        Windows Forms application, metadata parsers and guarded exports
macos/      Native AppKit preview and application bundle metadata
tests/      Synthetic regression/security tests; excluded from release builds
scripts/    Offline build and test entry points
docs/       Index behavior, privacy, review and publishing notes
.github/    Windows CI, issue forms and pull-request template
```

## License and references

MIT — see [LICENSE](LICENSE). Steam trademarks belong to Valve. Format references and the distinction between references and shipped dependencies are documented in [THIRD_PARTY_NOTICES.md](THIRD_PARTY_NOTICES.md).
