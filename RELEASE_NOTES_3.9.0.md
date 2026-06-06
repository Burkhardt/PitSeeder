# PitSeeder 3.9.0 Release Notes

## Summary

- Releases `PitSeeder` version `3.9.0`.
- Aligns CLI package metadata and fallback package references to `JsonPit 3.9.0` and `OsLibCore 3.9.0`.
- Refreshes release-note pointers in the packaged README for the coordinated minor line.
- No CLI behavior changes from `3.8.15`.

## Validation

- `dotnet build pits/pits.csproj --nologo -v minimal`
- NuGet publishing is handled by the tag-triggered `publish-pitseeder-nuget.yml` workflow in `RAIkeep`.
