# PitSeeder 3.7.6 Release Notes

Built and validated against:

- OsLib 3.7.6
- JsonPit 3.7.6
- .NET 10.0

## Summary

- Keeps the current CLI feature set intact.
- Adds explicit NuGet package icon metadata so the published package can display the RAIkeep logo.
- Aligns fallback package references with `JsonPit 3.7.6` and `OsLibCore 3.7.6`.

## Packaging Changes

- `pits.csproj` now emits `<icon>HardCastle.png</icon>` in the package manifest.
- `HardCastle.png` is packed into the root of the `.nupkg` so NuGet can render the logo.
- `Authors` metadata is emitted correctly for the package manifest.

## Validation

- `dotnet pack PitSeeder/pits/pits.csproj --configuration Release --nologo -v minimal`
- Verified packaged contents include:
  - `PitSeeder.nuspec`
  - `README.md`
  - `HardCastle.png`