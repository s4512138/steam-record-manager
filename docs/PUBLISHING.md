# Publishing this repository

This folder is the source repository to publish. Do not upload the surrounding work/output directory: older bundles and local reports there are not part of this public package.

## Before the first push

1. Review `LICENSE` (MIT was selected for this package) and adjust the copyright holder if desired.
2. Run `scripts/test.ps1` on Windows. Do not claim a GitHub Actions result until the remote job actually runs.
3. Review the file list and search for credentials, real account identifiers, reports, personal paths, actual `.pb` data and media. Only `src`, `tests`, `scripts`, `docs`, `.github` and the repository metadata/docs belong in the source upload. `dist` is generated and ignored.
4. Create a repository in your own GitHub account. The suggested name is `steam-record-manager`; the final owner/name are your choice. No remote URL is hard-coded in these files.
5. Enable private vulnerability reporting in GitHub's repository security settings. The security policy deliberately contains no invented email address.
6. Review Actions settings, enable appropriate secret scanning/push protection when available, and protect your default branch with the `windows-tests` check if that fits your workflow.

The source package has not been pushed or published automatically. The ambient browser page for SteamTracking is a reference project, not the destination for this tool.

## Example local Git setup

From this source folder:

```powershell
git init -b main
git add .
git diff --cached --stat
git status
git commit -m "Initial public source release"
```

Inspect the staged files before committing. Add the remote URL shown by GitHub for **your** new repository, then push using your normal Git credentials. This guide intentionally does not embed an account, credential or destination URL.

## Release assets

Build the release binary, run tests and review the index-beta warning. A release zip should contain the application, license and user documentation, not test executables, real reports or metadata. Generate a SHA-256 checksum for a binary you actually publish. The supplied build is unsigned, and checksum matching only verifies that a file matches the one hashed; it does not establish safety.

The regular CI workflow builds/tests only. The separate `Build desktop release` workflow is manually dispatched, defaults to build-only, and can publish the `v1.4.0-preview.1` prerelease when its `publish` option is explicitly selected on `main`. All three platform builds must succeed first. The final job uploads Windows x64, macOS arm64 and macOS Intel app archives plus `SHA256SUMS.txt`; it is the only job granted repository write permission. Re-running publication for an existing tag fails instead of silently replacing assets.

macOS builds are ad-hoc signed, not Apple Developer ID signed or notarized. Publishing these preview files does not certify that Gatekeeper will allow downloaded launches. See [MACOS_PREVIEW.md](MACOS_PREVIEW.md) and keep this limitation visible in release notes. Never add signing private keys to source, artifacts or issue attachments.

GitHub itself hosts the repository and executes these workflows after an authorized action; this is separate from the native applications' offline runtime behavior.
