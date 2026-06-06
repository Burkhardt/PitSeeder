# PitSeeder 3.7.5 Release Notes

Built and validated against:

- OsLib 3.7.5
- JsonPit 3.7.5
- .NET 10.0 (SDK 10.0.102)

## Breaking Changes

### `-d` / `--destination` removed

The destination parameter has been replaced by `-r` / `--pitroot`. The pit root is the base directory containing one or more canonical pits. For seeding, the pit name is derived from the source file; for export, it is given as a positional argument.

**Migration:**

```bash
# Old
pits -s Person.json5 -d output/Person.pit
pits -s sample/ -d output/ --wwwa

# New
pits -s Person.json5 -r output/
pits -s sample/ -r output/ --wwwa
```

## New Features

### Export to file (`-e`)

Export a pit's current state as a flat JSON file:

```bash
pits -r /pitroot/ Person -e ~/export/
```

Writes `Person.json` to the given directory using the `Pit.ExportJson` method.

### Export to stdout (`--json`)

Export a pit's current state directly to stdout for piping to tools like `jq` or `grep`:

```bash
pits -r /pitroot/ Person --json
pits -r /pitroot/ Person --json | jq '.[] | select(.Id == "Nomsa")'
```

### Positional pit name argument

The pit to operate on can be specified as a bare positional argument without a switch:

```bash
pits -r /pitroot/ Person --json
pits -r /pitroot/ Person -e /tmp/
```

### Pit root parameter (`-r` / `--pitroot`)

Replaces `-d`. Specifies the root directory containing canonical pits. All pit operations are resolved relative to this root.

```bash
pits -r /cloud/RAIkeep/WwwaTests/ Person --json
```

When combined with `-c` (cloud provider), the cloud storage root is prepended:

```bash
pits -c OneDrive -r RAIkeep/WwwaTests/ Person --json
```

### PitRoot inference from `-s`

When `-s` points to a `.pit` file and no `-r` is given, the pit root is inferred by stripping the canonical folder from the source path:

```bash
pits -s /cloud/WwwaTests/Person/Person.pit --json
# Infers pitroot as /cloud/WwwaTests/
```

### WWWA resolved export

Export all four WWWA pits with foreign key references resolved one level deep:

```bash
pits -r /pitroot/ --wwwa -e ~/export/     # writes wwwa.json
pits -r /pitroot/ --wwwa --json            # writes to stdout
```

Foreign key sections (`Who`, `What`, `Where`, `Activity`) are resolved against their corresponding pits (Person, Object, Place, Activity). When all references in a section resolve successfully, the wrapper dissolves and its contents are promoted to the item level. Unresolved wrappers remain unchanged.

Example: an Activity item referencing `Who: { Performer: "Nomsa" }` and `Where: { Venue: "SDZSafariPark" }` produces:

```json
{
  "Id": "SDZSP26",
  "Title": "Nomsa performing in the Elephant Valley",
  "Performer": { "Id": "Nomsa", "Name": "Nomsa Burkhardt", "Instruments": ["Voice", "Percussion", "Dance"] },
  "Venue": { "Id": "SDZSafariPark", "Name": "San Diego Zoo Safari Park", "Homepage": "https://sdzsafaripark.org/" }
}
```

### Cleaned-up help display

The help output now shows:

- All parameters with descriptions
- Resolved PitRoot path with existence status
- Status of all four WWWA pits under the given root
- Removed redundant `--source:` / `--destination:` summary lines

## Bug Fixes

### PitFile(string) path handling

Fixed `PitFile(string fullName)` constructor which was passing a full filename (e.g., `Person.pit`) to `new RaiPath()`, causing the filename to be treated as a directory segment. The resulting path would be `.../Person.pit/Person/Person.pit` instead of `.../Person/Person.pit`. Fixed by delegating to the `CanonicalFile(string)` base constructor, which properly separates the path from the filename.

### JsonPitPersistenceTests path handling

Fixed `new RaiPath(persistedFileFullName)` in the persistence test, which had the same issue as the PitFile constructor. Changed to `RaiPath.SplitRaiPathAndName(persistedFileFullName).path` to extract only the directory portion.

### Removed deleted Cloud enum and RemoteCloudSyncProbe references

Removed `CloudRemoteSyncTests.cs` and cleaned up `JsonPitRealWorldIntegrationTests.cs` to remove all references to the `Cloud` enum and `RemoteCloudSyncProbe` class, which were removed from OsLib as part of the RaiPath refactoring. The remaining cloud integration test now discovers providers via `Os.Config?.Cloud` directly.

### NuGet restore performance

Added `Directory.Build.props` at the repo root with `NuGetAudit=false` and `NuGetAuditMode=direct` to address extremely slow restore times (300-500 seconds) caused by .NET 10's new default of `NuGetAuditMode=all` for transitive dependency scanning.

## Test Results

All tests pass:

- OsLib: 56 passed, 0 failed
- JsonPit: 83 passed, 0 failed
