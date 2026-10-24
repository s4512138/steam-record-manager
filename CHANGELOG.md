# Changelog

## 1.4.0-preview.1 — 2026-10-24

- Added a native AppKit macOS preview: local scan/name lookup, three languages, filtering, protected items, retention suggestions and confirmed native Trash operations.
- Kept macOS indexes unchanged; no permanent-delete or CSV feature is advertised for macOS.
- Added Windows/Apple Silicon/Intel release packaging with synthetic tests, native UI initialization checks, pinned artifact actions and SHA-256 checksums.
- Moved end-user download/run instructions above developer build instructions, with explicit signing, Gatekeeper and platform-parity limits.

## 1.3.1 — 2026-10-24

- Prepared a source-only public repository with English/Traditional Chinese documentation, MIT license, privacy/security notes, Windows CI and contribution templates.
- Escaped formula-like CSV values; refused report output inside recording roots and command-line overwrite of existing reports.
- Used an explicit Windows Explorer executable path and quoted folder argument.
- Rejected UNC/device recording paths and junction-based report destinations; added a current-user execution manifest.
- Bounded appinfo input and protobuf field counts; replaced repeated array-prefix enumeration with direct byte copies.
- Isolated tests from the release binary, used unique index-test folders and removed release test-bypass entry points.
- Retained the Steam-exit requirement and opt-in index beta status.

## 1.3 — 2026-10-24

Added optional index synchronization, missing-local-reference repair, pre-write index backups and copy-based index regression tests.

## 1.2 — 2026-10-24

Added live Traditional Chinese, Simplified Chinese and English language selection and preference persistence.

## 1.1 — 2026-10-24

Added local Steam appinfo cache lookup for game names not found in installed-game manifests.

## 1.0 — 2026-10-24

Initial session-level space viewer, cleanup previews, protection flags, CSV export and confirmed file cleanup.
