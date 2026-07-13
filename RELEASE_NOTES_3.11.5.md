# PitSeeder 3.11.5 Release Notes

## Summary

- Releases `PitSeeder` version `3.11.5`.
- Aligns CLI package metadata and fallback package references to `JsonPit 3.11.5` and `OsLibCore 3.11.5`.
- Keeps `PitSeeder` last in the coordinated release order, immediately after `ImgSeeder`/`iorg`.
- Carries forward `pits` subscriber identity behavior and local ticket-window regression coverage from `3.11.4`.

## Validation

- `dotnet build pits/pits.csproj --nologo -v minimal`
- NuGet publishing remains wired through the parent sequential release chain and the tag-triggered `publish-pitseeder-nuget.yml` workflow in `RAIkeep`.
