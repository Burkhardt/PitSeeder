# Release Notes 3.7.8

## Summary

- Patch release for the `PitSeeder` .NET tool package version `3.7.8`.
- This is a version-symmetry release aligned with `JsonPit 3.7.8` and `OsLibCore 3.7.8`.
- No breaking CLI changes.

## Package Alignment

- `pits/pits.csproj` now defaults to:
  - `JsonPitPackageVersion = 3.7.8`
  - `OsLibPackageVersion = 3.7.8`
- Tool package version updated to `3.7.8`.

## CLI Contract

- Tool command name remains `pits`.
- Existing options and WWWA behavior are unchanged.

## Validation

- `dotnet test PitSeeder.slnx --nologo -v minimal`
