# PitSeeder 3.11.1 Release Notes

## Summary

- Releases `PitSeeder` version `3.11.1`.
- Aligns CLI package metadata and fallback package references to `JsonPit 3.11.1` and `OsLibCore 3.11.1`.
- Keeps `PitSeeder` last in the coordinated release order, immediately after `ImgSeeder`/`iorg`.
- Uses explicit `pits` subscriber identity for JsonPit participation, so CLI writes claim and validate master tickets as `Machine-pits` consistently (including `dotnet pits.dll` execution).
- Adds a process-level local ticket-window regression test that verifies API/CLI short-sequence conflict handling and change-file merge safety.

## Validation

- `dotnet build pits/pits.csproj --nologo -v minimal`
- NuGet publishing remains wired through the parent sequential release chain and the tag-triggered `publish-pitseeder-nuget.yml` workflow in `RAIkeep`.
