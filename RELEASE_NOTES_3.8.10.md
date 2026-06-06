# PitSeeder 3.8.10 Release Notes

## Summary

- Fixes cloud-relative pitroot resolution in the `pits` CLI.
- When `-c <provider>` and `-r <pitroot>` are used together, `-r` is now resolved as a provider-relative `RaiRelPath` under the configured cloud root.
- Preserves non-cloud `-r` behavior, so absolute/local pitroot usage remains supported when `-c` is absent.
- Preserves the previously working cloud shorthand with a leading slash, such as `-r /LiveAfricaStage`.

## Fixed

The following commands now resolve to the same configured OneDrive pit root:

```bash
pits -h -c OneDrive -r LiveAfricaStage
pits -h -c OneDrive -r LiveAfricaStage/
pits -h -c OneDrive -r /LiveAfricaStage
```

Expected resolved root:

```text
/Users/RSB/Library/CloudStorage/OneDrive/OneDriveData/LiveAfricaStage/
```

## Validation

- Added `PitSeeder/pits.Tests` with regression coverage for the three cloud-relative pitroot forms.
- `dotnet test PitSeeder/PitSeeder.slnx --nologo -v minimal`
- `dotnet test RAIkeep.slnx --nologo -v minimal`
