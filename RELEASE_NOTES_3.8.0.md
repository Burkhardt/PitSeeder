# Release Notes 3.8.0

## Summary

- Release for the `PitSeeder` .NET tool package version `3.8.0`.
- Publishes the NuGet tool package from the `RAIkeep` repository via GitHub Actions.
- Publishes self-contained `pits` binaries for `osx-arm64`, `osx-x64`, `linux-x64`, and `win-x64` as GitHub release assets.
- Aligns fallback package version defaults with `JsonPit 3.8.0` and `OsLibCore 3.8.0`.

## CI/CD

- Workflow: `.github/workflows/publish-pitseeder-nuget.yml`
- Trigger: push a tag in format `v*`
- Guardrail: tag version must match `<Version>` in `PitSeeder/pits/pits.csproj`
- NuGet publish target: `https://api.nuget.org/v3/index.json`
- Self-contained targets: `osx-arm64`, `osx-x64`, `linux-x64`, and `win-x64`
- Self-contained outputs are uploaded as GitHub release assets on the matching tag

## Validation

- `dotnet pack PitSeeder/pits/pits.csproj --configuration Release`
- `dotnet publish PitSeeder/pits/pits.csproj --configuration Release --runtime osx-arm64 --self-contained true /p:PublishSingleFile=true`
- `dotnet publish PitSeeder/pits/pits.csproj --configuration Release --runtime osx-x64 --self-contained true /p:PublishSingleFile=true`
- `dotnet publish PitSeeder/pits/pits.csproj --configuration Release --runtime linux-x64 --self-contained true /p:PublishSingleFile=true`
- `dotnet publish PitSeeder/pits/pits.csproj --configuration Release --runtime win-x64 --self-contained true /p:PublishSingleFile=true`
- Result: tool package packed successfully with `README.md`, `HardCastle.png`, and `DotnetToolSettings.xml`; self-contained single-file publishes succeeded for `osx-arm64`, `osx-x64`, `linux-x64`, and `win-x64`.