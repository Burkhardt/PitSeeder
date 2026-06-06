# Release Notes 3.7.9

## Summary

- Patch release for the `PitSeeder` .NET tool package version `3.7.9`.
- Publishes the NuGet tool package from the `RAIkeep` repository via GitHub Actions.
- Publishes self-contained `pits` binaries for `osx-arm64`, `osx-x64`, `linux-x64`, and `win-x64` as GitHub release assets.
- Aligns fallback package version defaults with `JsonPit 3.7.9` and `OsLibCore 3.7.9`.

## CI/CD

- Workflow: `.github/workflows/publish-pitseeder-nuget.yml`
- Trigger: push a tag in format `v*`
- Guardrail: tag version must match `<Version>` in `PitSeeder/pits/pits.csproj`
- NuGet publish target: `https://api.nuget.org/v3/index.json`
- Self-contained targets: `osx-arm64`, `osx-x64`, `linux-x64`, and `win-x64`
- Self-contained outputs are uploaded as GitHub release assets on the matching tag

## Validation

- `dotnet publish PitSeeder/pits/pits.csproj --configuration Release --runtime linux-x64 --self-contained true /p:PublishSingleFile=true --output PitSeeder/artifacts/publish/linux-x64-ci-smoke-nooverride`
- Result: local self-contained publish succeeded.
