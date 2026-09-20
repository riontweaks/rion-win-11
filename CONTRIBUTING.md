# Contributing

- Use Windows x64 and the .NET 8 SDK. Run `tools/Build.ps1` and `tools/Test.ps1` before submitting changes.
- Preserve the single self-contained EXE release and existing third-party attribution.
- Keep generated builds, downloaded installers, logs, credentials and signing material out of Git.
- Every tweak needs an accurate explanation, current-state inspection and exact restore behavior, including restoration of an absent registry value.
- Test system writes with fake state. Do not use somebody's live registry, services, adapters or GPU as an automated test fixture.
- Unsupported hardware capabilities must remain unavailable rather than reporting fake success.
- Use the existing shared ShellRunner for application process execution.
- Never overwrite another contributor's working changes. During migration, sync using the manifest and review conflicts.
- Reports should distinguish compilation, fake-state tests, read-only observations and actual hardware validation.

Focused example:

```powershell
dotnet test './src/RionSuite/tests/Startup/Startup.Tests.csproj' -m:1
```

The supported regression command and its selected suites are documented in [docs/TESTING.md](docs/TESTING.md). Some legacy/experimental tests may have additional fixture or compile switches. Read their project files and module documentation before invoking them.
