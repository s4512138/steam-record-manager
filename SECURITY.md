# Security policy

## Supported version

Only the latest release receives fixes. The current release line is 1.4.0-preview.1. Windows index editing remains beta and is not yet verified by a live Steam reload test. The new macOS preview does not edit indexes and is not Developer ID signed or notarized; see docs/MACOS_PREVIEW.md for its narrower validation scope.

## Reporting a vulnerability

Use the repository's **Security → Report a vulnerability** private channel if the maintainer has enabled GitHub private vulnerability reporting. Do not publish an exploit, a real index, a recording, a full path report, or a credential in a public issue.

If that private channel is unavailable, open a non-sensitive issue asking the maintainer to enable it, without technical exploit details or personal data. This repository intentionally does not invent a maintainer email address. Response times are best effort; no SLA is promised.

Include the application version, Windows/.NET versions, a description of the impact, and a minimal synthetic reproduction. Redact usernames, Steam identifiers, paths, game histories and any credentials.

## Security boundary

The application runs as the interactive user, never requires elevation, and is not a privileged service. It is intended for trusted local recording directories. It does not defend against an attacker already able to modify the same user's files or continuously race filesystem changes. Process-name checks are a precaution, not synchronization with Steam.

Review [the security assessment](docs/SECURITY_REVIEW.md) for findings, mitigations and remaining limitations. A passing CI run or source review is not a declaration that the software has no vulnerabilities.
