# PitSeeder 3.13.0 Release Notes

## Summary

- Releases `PitSeeder` version `3.13.0`.
- Aligns CLI package metadata and fallback package references to `JsonPit 3.13.0` and `OsLibCore 3.13.0`.
- Keeps `PitSeeder` last in the coordinated release order, immediately after `ImgSeeder`/`iorg`.
- Carries forward `pits` subscriber identity behavior and local ticket-window regression coverage from `3.12.0`.

## Validation

- `dotnet test PitSeeder.slnx --nologo -v minimal`
- NuGet publishing remains wired through the parent sequential release chain and the tag-triggered `publish-pitseeder-nuget.yml` workflow in `RAIkeep`.
