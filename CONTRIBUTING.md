# Contributing

Use Windows 10/11, .NET Framework 4.8 and PowerShell. No NuGet restore is needed.

```powershell
.\scripts\build.ps1
.\scripts\test.ps1
```

The build treats compiler warnings as errors. The test build defines `TESTS` and has a separate console entry point; the release build excludes test suites and their bypass helpers. Tests run on newly generated temporary fixtures, including a directory junction, and leave those fixtures for inspection. Do not point destructive tests at real recordings.

## Change expectations

- Keep all runtime operations local and at the user's privilege level.
- Preserve confirmations, protected-item handling, root boundaries and Steam-exit checks.
- Add targeted regressions for parser, path, deletion or index behavior changes.
- Include Traditional Chinese, Simplified Chinese and English translations with identical format placeholders.
- Treat Steam manifests, caches, indexes and filenames as data, not commands.
- Preserve unknown protobuf fields. Do not infer that an empty timeline or missing cloud-backed folder is disposable.
- Keep tests, reports, indexes, recordings, executable builds and personal logs out of commits.

For a bug report, use synthetic data or a redacted description. For a suspected vulnerability, follow [SECURITY.md](SECURITY.md). Pull requests should describe the trigger, resulting behavior, tests and any remaining compatibility limits.

The current code uses the framework-bundled C# compiler and its language features; avoid requiring a newer compiler without updating the offline build instructions. The compact legacy methods are candidates for gradual readability refactoring, separate from behavior changes.
