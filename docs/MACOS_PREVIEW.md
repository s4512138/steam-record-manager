# macOS preview — 1.4.0-preview.1

繁中：macOS 原生預覽版支援容量掃描、遊戲名稱、篩選、保護標記、保留規則與移至垃圾桶。**不修改 Steam 索引、不提供永久刪除及 CSV 報表。** 使用前完全退出 Steam。解壓縮後開啟 `Steam Record Manager.app`；Apple Silicon 使用 arm64 檔，Intel 使用 x64 檔。需要 macOS 13 以上，不需要 Python、.NET 或開發工具。

English: This is a native AppKit preview for macOS 13+, built separately for Apple Silicon and Intel. Unzip the corresponding asset and open `Steam Record Manager.app`. You may move it to Applications. No interpreter or developer tools are required on the user's computer.

## Scope

- Traditional Chinese, Simplified Chinese and English, saved in app preferences.
- Local recording root selection, session sizes, modification dates and local Steam game-name lookup.
- Search, protected-session flags, size/age suggestions and final selection preview.
- Native macOS Trash operation after typing `TRASH`; no fallback to permanent deletion.
- Steam must be fully exited; source folder, metadata changes, recent writes and symlinks are checked before cleanup.

It does not edit `gamerecording.pb`, timeline JSON or clip metadata; Steam may retain old entries after files are moved to Trash. It does not provide the Windows CSV/report CLI or index beta features. Sessions are moved as complete folders, never individual `.m4s` fragments.

The app has no network client, updater, account sign-in, injection or shell execution. Language/protected path preferences use the `org.steamrecordmanager.preview` UserDefaults domain. Those paths are private local data.

## Signing and Gatekeeper

Release builds have **ad-hoc signatures only**. They are not signed with an Apple-issued Developer ID certificate and are not notarized. macOS can block downloaded apps; these builds do not promise a warning-free, default-Gatekeeper launch. Do not disable Gatekeeper or remove security attributes using terminal commands. If your Mac requires a notarized application, wait for a properly signed/notarized release.

Developer ID signing and notarization require the maintainer's Apple Developer credentials and certificate. They are not fabricated, bundled or created by this project. See [Apple's distribution guidance](https://developer.apple.com/developer-id/).

## Validation and limitations

The release workflow compiles on native Apple Silicon and Intel GitHub runners. Before publishing it runs synthetic core tests (metadata, retention/protection, path bounds, change detection, symlinks and appinfo formats), an AppKit window/control initialization smoke test and ad-hoc signature verification.

These checks do not replace visual testing or a real Steam library cleanup/recovery test on a user's Mac. Native Trash interaction, real recording compatibility and downloaded-app Gatekeeper behavior remain unverified manually. No real user recording is deleted by the test suite.

Checks are point-in-time, not protection against a malicious same-user process racing filesystem changes. Batches are not transactions: previously moved sessions stay in Trash after a later failure. Review the partial-completion message. Moving to Trash on the same volume does not free space until Trash is emptied; do that yourself only after checking the content.
