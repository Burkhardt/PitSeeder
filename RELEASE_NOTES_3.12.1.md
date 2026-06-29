# PitSeeder 3.12.1 Release Notes

## Summary

- Releases `PitSeeder` version `3.12.1`.
- Aligns CLI package metadata and fallback package references to `JsonPit 3.12.1` and `OsLibCore 3.12.1`.
- Keeps `PitSeeder` last in the coordinated release order, immediately after `ImgSeeder`/`iorg`.
- Carries forward `pits` subscriber identity behavior and local ticket-window regression coverage from `3.12.0`.

## Validation

- `dotnet build pits/pits.csproj --nologo -v minimal`
- NuGet publishing remains wired through the parent sequential release chain and the tag-triggered `publish-pitseeder-nuget.yml` workflow in `RAIkeep`.
