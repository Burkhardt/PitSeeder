# PitSeeder 3.8.14 Release Notes

## Summary

- Releases `PitSeeder` version `3.8.14`.
- Aligns CLI package metadata and fallback package references to `JsonPit 3.8.14` and `OsLibCore 3.8.14`.
- Refreshes release-note pointers in the packaged README for the coordinated chain.

## Validation

- `dotnet build pits/pits.csproj --nologo -v minimal`
- NuGet publishing is handled by the tag-triggered `publish-pitseeder-nuget.yml` workflow in `RAIkeep`.
