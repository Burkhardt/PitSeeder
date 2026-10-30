# PitSeeder

## Latest Version 4.5.15

Help now follows the shared CLI theme. Use -w/--window and -x/--id-exists;
previous spellings remain accepted as unadvertised compatibility aliases.

Latest release notes: [PitSeeder 4.5.15 release notes](https://github.com/Burkhardt/RAIkeep/blob/main/doc/releases/v4.x/PitSeeder_RELEASE_NOTES_4.x.md#4515).

## Previous Versions

For release notes and changelogs of earlier releases, see the [PitSeeder 4.x Release Notes](https://github.com/Burkhardt/RAIkeep/blob/main/doc/releases/v4.x/PitSeeder_RELEASE_NOTES_4.x.md).

## Terminal font

> **Font note:** The `pits` help screen uses glyph icons from Nerd Fonts. Most
> Nerd Font-patched fonts render correctly in most terminal environments. Blink
> on iPadOS showed clipping and character-width problems with some choices; the
> tested solution was Blink's
> [Jet Brains Mono Nerd Font stylesheet](https://github.com/blinksh/patched-fonts/blob/main/Jet%20Brains%20Mono%20Nerd%20Font.css).
> See the RAIkeep
> [terminal font guide](https://github.com/Burkhardt/RAIkeep/blob/main/doc/TERMINAL_FONTS.md)
> for Blink, macOS, and Ubuntu setup.

PitSeeder change requests and release notes are centralized in the RAIkeep [`doc/`](https://github.com/Burkhardt/RAIkeep/tree/main/doc) directory under `PitSeeder_...` filenames; they are not stored separately in this child repository.

PitSeeder uses the shared RAIkeep configured cloud-root contract: `Dropbox`, `OneDrive`, `GoogleDrive`, and `ICloudDrive`.

PitSeeder (`pits`) is a .NET command-line tool for working with [JsonPit](https://github.com/Burkhardt/RAIkeep) data stores. It can seed pits from JSON/JSON5 source files, export pits to JSON, and produce resolved WWWA exports where foreign key references are expanded inline.

Operational recovery-log guidance: [`PITS-AUDIT.md`](https://github.com/Burkhardt/RAIkeep/blob/main/doc/PITS-AUDIT.md).

Within this repository, PitSeeder lives under `RAIkeep/PitSeeder` so it can build against the local `JsonPit` and `OsLib` sources before those packages are published.


## Installation

### .NET global tool

```bash
dotnet tool install --global PitSeeder
```

To update:

```bash
dotnet tool update --global PitSeeder
```

*(Requires `~/.dotnet/tools` on your PATH.)*

### .NET tool in `/usr/local/bin` (Recommended for macOS & Linux)

Install directly to `/usr/local/bin` so no `$PATH` modification is needed:

```bash
sudo dotnet tool install PitSeeder --tool-path /usr/local/bin
```

To update:

```bash
sudo dotnet tool update PitSeeder --tool-path /usr/local/bin
```

> **Note:** `sudo` is only required during installation and updates to write to `/usr/local/bin`. Running the installed CLI command never requires `sudo` or elevated privileges.

## CLI Reference

```text
pits seed (<PitName> | --wwwa) --source <file-or-directory> [global options]
pits export (<PitName> | --wwwa) (--out-dir <dir> | --json) [--at <timestamp>] [global options]
pits audit (<PitName> | --wwwa) [--machine <filter>] [--level <severity>] [--json] [global options]
pits delete-property <PitName> <ItemId> <PropertyPath> [global options]
pits delete-item <PitName> <ItemId> [global options]
pits maintain (<PitName> | --wwwa) [--apply] [--archive-events] [--json] [global options]
  [--prune-process-flags --older-than <duration>] [--repair-legacy-extensions]
```

| Global option | Description |
|--------|-------------|
| `-h`, `--help` | Print all options with resolved paths |
| `-v`, `--version` | Print version info |
| `-n`, `--nologo` | Suppress the banner |
| `-b`, `--debug` | Enable debug output |
| `-r`, `--pitroot` | Root directory containing pits; when used with `-c`, this is relative to the configured cloud root |
| `-c`, `--cloud` | Cloud provider name (looks up root in `~/.config/RAIkeep.json5`) |
| `-w`, `--window` | Keep this CLI process activity window until timeout |

`--source` belongs to `seed`; `--out-dir` and `--at` belong to `export`; `--machine` and
`--level` belong to `audit`. `--json` is available on `export`, `audit`, and `maintain`.
Run `pits <command> --help` for contextual help.

### Maintain a pit

`maintain` is report-only unless `--apply` is present:

```bash
pits maintain Activity -r /path/to/pitroot --json
pits maintain Activity -r /path/to/pitroot --apply
pits maintain --wwwa -r /path/to/pitroot --apply
```

Ordinary apply reconciles valid changes, creates or preserves immutable cleanup
receipts, and retires eligible pairs only after the grace and authority checks.
Removing proven expired PID-specific process windows additionally requires
`--prune-process-flags --older-than <duration>`. Repairing recognizable,
content-valid extensionless process flags or recovery events additionally requires
`--repair-legacy-extensions`. Unknown, malformed, live, master, and conflict files
are retained.

One command can perform ordinary WWWA maintenance and explicit process-window
pruning together:

```bash
pits maintain --wwwa -c OneDrive -r AIA \
  --apply \
  --prune-process-flags \
  --older-than 01:00:00 \
  --json
```

Do not follow this with a separate `pits maintain --wwwa ... --apply`; the
combined invocation already merges changes, handles receipts, retires eligible
pairs, and prunes eligible process flags.

Normal writable JsonPit construction with `autoload: true` also applies ordinary
change/receipt maintenance when that instance obtains exact-master authority.
This preserves the original "master opening performs cleanup" behavior, but it
does not create a background timer: a receipt's ten-minute grace must have
elapsed before a later opening or maintenance pass can delete its pair. A
long-running service that opens each pit only once therefore needs a periodic
maintenance call if it should retire eligible pairs without waiting for its next
restart.

Process-flag pruning remains deliberately explicit and is never performed merely
because a master opens a pit. `--prune-process-flags` currently requires
`--apply`; ordinary report-only maintenance inventories active, released, and
naturally expired flags, but does not preview the exact prune/defer decision.
Recovery events are durable audit records and are not deleted by ordinary
`maintain`. Their physical compaction is separately requested with
`--archive-events`.

Preview one pit’s event compaction without writing or deleting anything:

```bash
pits maintain Object -c OneDrive -r AIA --archive-events --json
```

Apply it, or compact all existing WWWA pits:

```bash
pits maintain Object -c OneDrive -r AIA \
  --apply --archive-events --json

pits maintain --wwwa -c OneDrive -r AIA \
  --apply --archive-events --json
```

The archive is named from the oldest and newest validated event timestamps in
UTC, such as `Events_20260804-0118_to_20260910-1643.zip`. It is created directly
inside the existing pit `Events` directory. There is no TempDir staging,
directory replacement, archive overwrite, or extraction. Loose files are
removed individually only after the archive validates their exact filenames and
bytes. Corrupt files, collisions, and evidence that changes during the operation
are retained and reported in `Deferred` or `Failures`.

### Delete a nested property or item

`delete-property` interprets `PropertyPath` as a dot-delimited JSON path. It
appends a tombstone without overwriting sibling properties:

```bash
pits delete-property Activity UC16_SavePits_DevSession What.Chat -c OneDrive -r AIA -n
```

`delete-item` appends the established item tombstone:

```bash
pits delete-item Object LegacyRecord -c OneDrive -r AIA -n
```

Both commands save before returning success, preserve append-only history, and
release the normal process activity window. `-n` continues to mean
`--nologo`; it does not suppress persistence.

When `-c`/`--cloud` is supplied, the provider must occur in
`Os.Config.DefaultCloudOrder` and have a non-empty entry in `Os.Config.Cloud`.
Having only a `Cloud.<provider>` path does not enable that provider for CLI use;
matching is case-insensitive and the configured spelling is retained.

## Events audit mode

`pits audit <PitName> -r <root>` reads the pit's durable recovery events from its
`Events` child directory and its immutable `Events_*.zip` archives. It is strictly read-only: it opens no `Pit`, creates no
process or master flag, merges nothing, and writes no audit event. With `--json` it
emits the filtered events as a JSON array; otherwise output is human-readable and
ordered deterministically by machine, UTC time, and event identity. Duplicate
loose/archive copies are emitted once; conflicting identities cause a diagnostic
and a nonzero command result without deleting either source.

For stage meanings, severity behavior, incident workflows, JSON fields, and
practical `jq` reports, read the full
[`pits audit` operational manual](https://github.com/Burkhardt/RAIkeep/blob/main/doc/PITS-AUDIT.md).

```zsh
pits audit Person -n -r /path/to/pitroot/ --level warning
pits audit Person -n -r /path/to/pitroot/ --json | jq '.[].Stage'
```

Use `pits audit --wwwa` to aggregate the four WWWA event directories. Legacy
`--events`, `--event-machine`, and `--event-level` invocations fail with migration
guidance rather than being silently reinterpreted.

## 4.x legacy transition

Existing flat seed/export invocations remain supported in `4.x`, including `-s`,
`-e`, positional pit names, direct `.pit` input, and WWWA seed/export. Command-first
syntax is preferred for new scripts. The `5.x.x` line will require subcommands.

## Process-window lifecycle

Finite `pits` commands dispose their pits through JsonPit's durability boundary by default after normal completion and when execution unwinds through an exception: accepted fragments are exported as collision-safe change files before the process activity window is released. Ctrl+C and process exit also attempt this cleanup. Use `-w` / `--window` when the activity window should remain until timeout. The previous `--retain-window` spelling remains accepted for compatibility.

Each invocation uses `{MachineName}-pits-{PID}.flag`. Release succeeds only while the flag content still identifies that OS process, then removes that exact file through `RaiFile.rm()` (including the cloud disappearance wait). Another process's activity flag cannot be released. A crash or forced termination can still leave its flag for TTL-based inspection and explicit maintenance pruning.

The process activity window is not the master writer ticket. A completed seed command retains its timed master ticket so stale API writers continue to fall back to change files; this preserves the existing overlapping-writer safety contract.

## Features

### Seed a single pit

Import a JSON5 file into a pit under the given pit root:

```bash
pits seed Person --source ./sample/Person.json5 -r ./output/
```

This creates `./output/Person/Person.pit`.

### Seed the WWWA set

Import all four WWWA files (Person, Object, Place, Activity) from a source directory:

```bash
pits seed --wwwa --source ./sample/ -r ./output/
```

### Export a single pit to a file

```bash
pits export Person -r /path/to/pitroot/ --out-dir ~/export/
```

Writes `~/export/Person.json` containing the projected current state of all items.

### Export a single pit to stdout

```bash
pits export Person -n -r /path/to/pitroot/ --json
```

Output:

```json
[
  {
    "Id": "Nomsa",
    "Name": "Nomsa Burkhardt",
    "Instruments": ["Voice", "Percussion", "Dance"],
    "Deleted": false,
    "Modified": "2026-04-06T04:06:10.349+00:00"
  },
  ...
]
```

### Pipe to jq

Because `--json` writes to stdout, standard UNIX piping works (just add -n to suppress the banner):

```bash
pits export Person -n -r /path/to/pitroot/ --json | jq '.[] | select(.Id == "Nomsa") | .Instruments'
```

```json
["Voice", "Percussion", "Dance"]
```

### Export a point in time

Add `--at` to either a single-pit or WWWA export. The timestamp must include
`Z` or a numeric UTC offset. Projection is inclusive: changes timestamped
exactly at the cutoff are included.

```bash
pits export Person -n -r /path/to/pitroot/ --json --at 2026-08-27T12:00:00Z
```

The output is strict JSON and gains an envelope only for an `--at` export:

```json
{
  "_export": {
    "at": "2026-08-27T12:00:00.0000000Z",
    "exported": "2026-08-31T18:42:03.1234567Z"
  },
  "data": [
    { "Id": "Nomsa", "Name": "Nomsa Burkhardt" }
  ]
}
```

`at` is the requested historical cutoff. `exported` is when this invocation
constructed the document from the history then available. It is not a
distributed synchronization barrier. A later invocation with the same `at`
can legitimately differ after a delayed or backdated change arrives.

Items that did not yet exist or were deleted at the cutoff are omitted. In a
WWWA export, a missing projected target is absent from the lookup while the
source reference remains safely unresolved; export does not rewrite it. The
existing filenames (`Person.json` and `wwwa.json`) remain unchanged for
`--out-dir` exports because provenance is inside the document.

Without `--at`, the established unwrapped array/object output remains
unchanged. With `--at`, select through `.data` when piping:

```bash
pits export Person -n -r /path/to/pitroot/ --json \
  --at 2026-08-27T12:00:00+00:00 \
  | jq '.data[] | select(.Id == "Nomsa")'
```

### Export all WWWA pits with resolved foreign keys

```bash
pits export --wwwa -r /path/to/pitroot/ --out-dir ~/export/
```

Writes `~/export/wwwa.json` with all four pits merged into a single JSON object. Foreign key references in `Who`, `What`, `Where`, and `Activity` sections are resolved one level deep. Resolved wrappers dissolve and their contents are promoted to the item level; unresolved wrappers remain as-is.

For example, an Activity item with:

```json
{
  "Who": { "Performer": "Nomsa" },
  "Where": { "Venue": "SDZSafariPark" }
}
```

becomes:

```json
{
  "Performer": { "Id": "Nomsa", "Name": "Nomsa Burkhardt", "Instruments": ["Voice", "Percussion", "Dance"] },
  "Venue": { "Id": "SDZSafariPark", "Name": "San Diego Zoo Safari Park", "Homepage": "https://sdzsafaripark.org/" }
}
```

The same works with stdout:

```bash
pits export --wwwa -n -r /path/to/pitroot/ --json | jq '.Place[] | select((.Id | startswith("SD")) or (.Name | contains("Zoo"))) | {Id, Name}'
```

```json
{
  "Id": "SanDiegoZoo",
  "Name": "San Diego Zoo"
}
{
  "Id": "SDZSafariPark",
  "Name": "San Diego Zoo Safari Park"
}
```

### Cloud provider support

Use `-c` to look up a cloud storage root from `~/.config/RAIkeep.json5`:

```bash
pits export Person -c OneDrive -r LiveAfricaStage --json
```

This resolves the cloud root from the config and appends the `-r` value as a provider-relative path. If `OneDrive` is configured as:

```text
/Users/RSB/Library/CloudStorage/OneDrive/OneDriveData/
```

then these forms all resolve to the same pit root:

```bash
pits -c OneDrive -r LiveAfricaStage
pits -c OneDrive -r LiveAfricaStage/
pits -c OneDrive -r /LiveAfricaStage
```

Resolved pit root:

```text
/Users/RSB/Library/CloudStorage/OneDrive/OneDriveData/LiveAfricaStage/
```

### PitRoot inference

When `-s` points to a `.pit` file, the pit root is inferred automatically by stripping the canonical folder:

```bash
pits -s /cloud/RAIkeep/WwwaTests/Person/Person.pit --json
```

No `-r` needed.

### Help with resolved paths

Pass `-h` with other parameters to see all paths fully resolved:

```bash
pits -h -n -r /cloud/RAIkeep/WwwaTests/
```

The help display shows which pits exist at the given root.

## WWWA Data Model

WWWA stands for **W**ho, **W**hat, **W**here, **A**ctivity. It is a convention for organizing data across four canonical pits:

| Section keyword | Resolves against pit |
|-----------------|---------------------|
| `Who` | Person |
| `What` | Object |
| `Where` | Place |
| `Activity` | Activity |

Items in any pit can reference items in other pits using these section keywords. The values are Ids that correspond to items in the target pit.

## Build and Publish

- Coordinated release order: `Amafu -> OsLibCore -> RaiUtils -> RaiImage -> RaiDiagram -> RaidSeeder -> JsonPit -> ImgSeeder -> PitSeeder`

When a matching tag is pushed from the `RAIkeep` repository, the GitHub Actions workflow at `.github/workflows/publish-nuget.yaml` now:

- publishes the `PitSeeder` NuGet tool package from `PitSeeder/pits/pits.csproj`
- builds self-contained single-file `pits` binaries for `osx-arm64`, `osx-x64`, `linux-x64`, and `win-x64`
- uploads those self-contained binaries as GitHub release assets on the matching tag

See [BuildFromSource.md](https://github.com/Burkhardt/PitSeeder/blob/main/BuildFromSource.md) for:

- building from source
- building inside the `RAIkeep` workspace against local projects
- packing and publishing the NuGet tool
- publishing self-contained binaries for macOS, Ubuntu, and Windows


## License

This project is licensed under the Apache 2.0 license. See [LICENSE](LICENSE).
