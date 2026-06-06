# PitSeeder 3.7.7 Release Notes

Built and validated against:

- OsLib 3.7.7
- JsonPit 3.7.7
- .NET 10.0

## Summary

- Keeps the current CLI feature set intact.
- Aligns fallback package references with `JsonPit 3.7.7` and `OsLibCore 3.7.7`.
- Refreshes current package docs for the `3.7.7` release line.

## Packaging

- `pits.csproj` now points its fallback package versions at the `3.7.7` line.
- `README.md` now points at the `3.7.7` release notes.

## Validation

- `dotnet test RAIkeep.slnx --nologo -v minimal`
- Result: 214 passed, 0 failed, 0 skipped.