# Local security review — 1.3.1

Date: 2026-10-24. Scope: the source and scripts in this public package. This assessment was performed while preparing the repository, not by an independent auditor. It is a targeted review with regression tests, not a penetration test, malware scan, formal verification, or guarantee that no vulnerabilities exist.

## Threat model

The application runs as a non-elevated interactive Windows user. Inputs include a selected local recording root, filesystem entries, Steam text manifests, the binary appinfo cache, recording indexes, search/retention settings and report destinations. It intentionally deletes user-selected local recordings after confirmation. Steam's private schema and the Windows Recycle Bin are external compatibility dependencies.

There is no runtime HTTP client, token store, account authentication, browser integration, code injection, shell-command interpreter or dynamic package downloader in the application. Paths are not sent to an external service. No embedded credentials or personal scan data were found in the intended public source/docs allowlist. These statements apply to this reviewed package, not arbitrary future changes or downloaded executables.

## Findings addressed before public packaging

| Finding | Potential effect | Change and verification |
| --- | --- | --- |
| CSV values only used quoting | A crafted game name could be interpreted as a spreadsheet formula | Prefix formula-like or leading-control cells with an apostrophe; regressions cover `=`, `+`, `-`, `@`, whitespace and quoting |
| Report output could target source data or an existing file | Accidental corruption via CLI output or GUI export | Reject destinations inside the selected recording root; require a completed scan; reject linked destination parents; CLI uses new-file semantics; GUI replacement requires its Save dialog confirmation |
| Explorer resolved by executable name | Search-path ambiguity; unquoted folder argument | Use the Windows directory's `explorer.exe`, one quoted path, `UseShellExecute=false` |
| Fixed index-test fixture name | Repeated tests could overwrite unrelated files under a supplied test base | Unique test folders; tests moved to a separately compiled runner; production test/bypass APIs absent |
| Broad parser allocations / repeated prefix enumeration | Excess memory or CPU use on malformed local cache/index input | Appinfo size bound, existing nesting/string bounds, protobuf length/varint checks, a 100,000-field bound and direct bounded byte copies |
| Release privilege and supported path scope were implicit | Unnecessary elevation or confusing remote filesystem behavior | Explicit `asInvoker` manifest; local-drive recording/report paths only; junction refusal tested |
| Personal deliverables mixed with source | Public exposure of game history, paths or local metadata | Source/docs/scripts/templates-only publication allowlist; ignore binaries, reports, `.pb` files, recordings and local history |

## Existing controls reviewed

- Deletion is restricted to recognized immediate session folders under the selected `video` or `clips` directory.
- Root/ancestor reparse points and linked session contents are rejected.
- Protection flags, recent writes and scan-to-delete metadata changes prevent deletion. The fingerprint covers file names, lengths and timestamps, **not file contents**.
- UI cleanup requires Steam exit, a final item list and an explicit confirmation token. Core deletion and index commit also check Steam.
- Index synchronization is opt-in. A preflight checks index structure and rejects selected recordings with remote-reference fields.
- Index editing retains timelines/events and preserves raw bytes of retained unknown fields. Missing tag-only clip references are not automatically classified as local.
- Index writes save pre-change bytes, flush staged data, compare the current source snapshot and use file replacement. Readback checks detect an unexpected result.
- The release CLI only scans or audits. It does not expose deletion, index-writing, or test-only process-check bypass commands.
- CI has read-only repository permissions, a commit-pinned checkout action, no persisted checkout credentials and no release-publishing token.

## Tests executed locally

`scripts/test.ps1` compiles both release and test binaries with warnings treated as errors, then runs:

1. Recording scans, age/size suggestions, protected items, path bounds, recent/changed files and fixture-only deletion.
2. appinfo v39/v40/v41 parsing, Unicode names, exact name-path selection and malformed-entry isolation.
3. Three-language resources, format placeholders, live control translations, preserved selections/settings and preference reload.
4. Byte-identical index no-op, selected-reference changes, unknown-field retention, empty/shared timelines, clip references, missing-file repair, remote-reference preservation and malformed-input rejection.
5. Atomic index fixture replacement, exact backups, stale snapshot rejection and simulated Steam-running rejection.
6. CSV formula defense, export containment, existing-output refusal, confirmed export replacement, parser resource bounds and varint overflow.
7. An actual Windows directory-junction fixture: scan/export refusal and an unchanged outside sentinel.
8. Reflection of the release binary confirming test suites and test-bypass APIs are absent.

Tests operate on fresh synthetic temporary data. They do not delete or rewrite real Steam recordings or indexes. The GitHub-hosted CI job is supplied but has not been run by this local preparation.

## Remaining limitations

- **Index compatibility remains beta.** A live Steam client reload after a real index edit has not been validated. Correct protobuf framing alone does not establish full semantic compatibility.
- **Filesystem races remain.** Checks are point-in-time. There is no full handle-relative, race-proof recursive deletion or exclusive handshake with Steam. An adversary operating as the same user can race renames, links or process start/stop. Do not use elevated privileges or treat this as a multi-tenant security boundary.
- **No batch rollback.** Video deletion can succeed before index writing or logging fails. An I/O error can leave partially deleted content. Backups contain metadata, not recordings. Recovery requires reviewing the log and available backups.
- **Recycle Bin behavior is controlled by Windows.** It can offer permanent deletion when recycling is unsupported or too large. The user must cancel such a prompt if recoverability is required. No automated interactive Recycle Bin fallback test is claimed.
- **Metadata is private.** Reports, backup indexes and history can reveal game names, timestamps, paths and other Steam metadata. They are not encrypted by this application and use the current user's normal filesystem permissions.
- **Local resource exhaustion is not eliminated.** Large legitimate trees consume scan time/memory; input limits reduce but do not prove immunity to every denial-of-service scenario.
- Binaries are unsigned. No independent antivirus service submission, dependency advisory scan or code-signing provenance attestation was performed.

## Public-release recommendation

Publish source with the beta limitations visible; do not describe the tool as “certified secure” or promise zero data loss. Keep index features off by default. Review this document after behavior changes. Follow [SECURITY.md](../SECURITY.md) for vulnerability handling.
