# Release Notes 3.7.8.1

## Summary

- Patch release for the `PitSeeder` .NET tool package version `3.7.8.1`.
- Adds automated publication from the `RAIkeep` repository via GitHub Actions tag workflow.
- Keeps fallback package version alignment with `JsonPit 3.7.8` and `OsLibCore 3.7.8`.

## CI/CD

- New workflow: `.github/workflows/publish-pitseeder-nuget.yml`
- Trigger: push a tag in format `v*`
- Guardrail: tag version must match `<Version>` in `PitSeeder/pits/pits.csproj`
- Publish target: `https://api.nuget.org/v3/index.json`
- Self-contained targets: `osx-arm64`, `linux-x64`, and `win-x64`
- Self-contained outputs are uploaded as GitHub release assets on the matching tag

## Validation

- `dotnet test PitSeeder/PitSeeder.slnx --nologo -v minimal`
