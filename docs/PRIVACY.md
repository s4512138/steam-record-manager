# Privacy

The application does not implement HTTP requests, telemetry, sign-in, uploads, automatic update downloads, Steam injection or browser-debug access. It does not require a Steam API key. Local report destinations and recording roots are used; UNC/device paths are not supported.

## Data read

- File names, lengths and timestamps under the selected recording root.
- Local Steam installation paths from the registry, a running Steam process or standard installation directories.
- Game-library manifests and appinfo cache entries for game names.
- Root recording indexes and timeline files when index features or metadata backups require them.

## Data written

- `%LOCALAPPDATA%\SteamRecordManager\language.txt`: selected UI language.
- `protected.txt` in the same directory: protected recording-folder paths.
- `history` subdirectories: cleanup plans, logs, and pre-change metadata/index backups.
- Reports at the destination you select. The command line requires a new file outside the recording root.
- When explicitly confirmed: removal of selected session folders; beta index changes if enabled.

These files are not encrypted by the application. Access depends on Windows permissions and the current account. Reports and logs may reveal game names, recording dates, absolute paths and index metadata. Index backups may contain additional Steam metadata beyond the small subset this tool interprets.

The macOS preview is a separate native implementation with no report/index-write features. It reads the selected recording tree and local Steam metadata, and stores language/protected-path preferences in UserDefaults under `org.steamrecordmanager.preview`. It moves selected complete folders using macOS's native Trash API, without invoking a shell or transmitting data. Preference paths and their normal operating-system backups may reveal recording locations. They are not encrypted by the app.

No real reports, caches, recordings, index backups, screenshots or machine-specific logs are included in this source package. Do not add them to issues or commits without reviewing and redacting them. A `.gitignore` prevents common accidental additions but cannot prevent an explicit forced add or disclosure in pasted text.
