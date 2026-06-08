# PitSeeder 3.10.0 Release Notes

## Summary

- Releases `PitSeeder` version `3.10.0`.
- Aligns CLI package metadata and fallback package references to `JsonPit 3.10.0` and `OsLibCore 3.10.0`.
- Keeps `PitSeeder` last in the coordinated release order, immediately after `ImgSeeder`/`iorg`.
- No CLI behavior changes from `3.9.1`.

## Validation

- `dotnet build pits/pits.csproj --nologo -v minimal`
- NuGet publishing remains wired through the tag-triggered `publish-pitseeder-nuget.yml` workflow in `RAIkeep`.
