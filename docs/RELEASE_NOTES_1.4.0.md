# 1.4.0 Preview — Windows and macOS applications

Download a ready-to-run application archive under Assets. GitHub's “Source code” archives are for developers.

| Platform | Asset |
| --- | --- |
| Windows 10/11 x64 (.NET Framework 4.8) | `SteamRecordManager-windows-x64.zip` |
| macOS 13+ Apple Silicon | `SteamRecordManager-macos-arm64-preview.zip` |
| macOS 13+ Intel | `SteamRecordManager-macos-x64-preview.zip` |

Windows: extract and open `SteamRecordManager.exe`. macOS: extract and open `Steam Record Manager.app`; no Python, .NET or Xcode is needed. `SHA256SUMS.txt` contains archive checksums.

**macOS preview scope:** scan, local game names, search, protection flags, cleanup suggestions and moving complete sessions to native Trash. No index editing, permanent deletion or CSV reports. Windows retains the existing features; index editing remains opt-in beta. Fully exit Steam before cleanup on either platform.

**Signing:** Windows is unsigned. macOS is ad-hoc signed, not Apple Developer ID signed or notarized; Gatekeeper may block downloaded builds. Do not disable system security protections. A notarized, warning-free macOS installation is not promised by this release.

Publication requires successful Windows regression tests and native Apple Silicon/Intel compilation, synthetic tests, UI initialization and signature verification. This is not a claim of manual macOS visual testing, real Steam cleanup/recovery testing or validated Gatekeeper behavior. Index compatibility remains beta and has not been validated by reloading an edited live Steam index.

繁中：此版本提供 Windows 執行檔與 Apple Silicon／Intel 的 macOS 原生預覽版。Mac 支援容量掃描、篩選、保護標記、清理建議與垃圾桶清理，暫不修改索引。Mac 未經 Apple Developer ID 簽章及公證，可能受到 Gatekeeper 阻擋；請勿停用系統防護。兩平台清理前皆須完全退出 Steam。
