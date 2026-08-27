# PitSeeder

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

Within this repository, PitSeeder lives under `RAIkeep/PitSeeder` so it can build against the local `JsonPit` and `OsLib` sources before those packages are published.

## 4.2.3

- Implements accepted CR015 `delete-property` and `delete-item` commands.
- Aligns fallback dependencies on `JsonPit 4.2.3` and `OsLibCore 4.2.3`.
- Retains the `4.2.1` Nerd Font glyphs, Blink guidance, and terminal clipping tolerance unchanged.
- Current release notes: [PitSeeder_RELEASE_NOTES_4.2.3.md](https://github.com/Burkhardt/RAIkeep/blob/main/doc/PitSeeder_RELEASE_NOTES_4.2.3.md)

## 4.2.1

- Uses glyphs embedded in `JetBrainsMonoNLNerdFontPropo-Regular` for cloud-provider and numbered help options, avoiding fallback-font width differences.
- Reserves two terminal cells at the end of help lines for renderers such as Blink.
- Continues to depend on `JsonPit 4.2.0` and `OsLibCore 4.2.0`; no library package version changes are part of this CLI-only patch.
- Terminal setup guidance: [TERMINAL_FONTS.md](https://github.com/Burkhardt/RAIkeep/blob/main/doc/TERMINAL_FONTS.md)
- Current release notes: [PitSeeder_RELEASE_NOTES_4.2.1.md](https://github.com/Burkhardt/RAIkeep/blob/main/doc/PitSeeder_RELEASE_NOTES_4.2.1.md)

## 4.2.0

- Retains the command-first `seed`, `export`, and `audit` syntax introduced for CR006.
- Keeps established flat seed/export invocations working throughout `4.x`; the legacy parser is scheduled for removal in `5.x.x`.
- Moves the recent `--events`, `--event-machine`, and `--event-level` surface directly to `audit`, `--machine`, and `--level` without a legacy audit mode.
- Keeps `PitSeeder` last in the coordinated release order, immediately after `ImgSeeder`/`iorg`.
- Aligns fallback dependencies on `JsonPit 4.2.0` and `OsLibCore 4.2.0`; no PitSeeder CLI behavior changes from 4.1.0.
- Release notes: [PitSeeder_RELEASE_NOTES_4.2.0.md](https://github.com/Burkhardt/RAIkeep/blob/main/doc/PitSeeder_RELEASE_NOTES_4.2.0.md)

## Install

After the package is published to NuGet:

```bash
dotnet tool install --global PitSeeder
```

On macOS or Linux, a practical option is to install directly into a directory on your `PATH`:

```bash
sudo dotnet tool install PitSeeder --tool-path /usr/local/bin
```

To update:

```bash
sudo dotnet tool update PitSeeder --tool-path /usr/local/bin
```

## CLI Reference

```text
pits seed (<PitName> | --wwwa) --source <file-or-directory> [global options]
pits export (<PitName> | --wwwa) (--out-dir <dir> | --json) [global options]
pits audit (<PitName> | --wwwa) [--machine <filter>] [--level <severity>] [--json] [global options]
pits delete-property <PitName> <ItemId> <PropertyPath> [global options]
pits delete-item <PitName> <ItemId> [global options]
```

| Global option | Description |
|--------|-------------|
| `-h`, `--help` | Print all options with resolved paths |
| `-v`, `--version` | Print version info |
| `-n`, `--nologo` | Suppress the banner |
| `-b`, `--debug` | Enable debug output |
| `-r`, `--pitroot` | Root directory containing pits; when used with `-c`, this is relative to the configured cloud root |
| `-c`, `--cloud` | Cloud provider name (looks up root in `~/.config/RAIkeep.json5`) |
| `--retain-window` | Keep this CLI process activity window until the normal timeout instead of releasing it on exit |

`--source` belongs to `seed`; `--out-dir` belongs to `export`; `--machine` and
`--level` belong to `audit`. `--json` is available on `export` and `audit`.
Run `pits <command> --help` for contextual help.

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
`Events` child directory. It is strictly read-only: it opens no `Pit`, creates no
process or master flag, merges nothing, and writes no audit event. With `--json` it
emits the filtered events as a JSON array; otherwise output is human-readable and
ordered deterministically by machine, UTC time, and event identity.

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

Finite `pits` commands dispose their pits through JsonPit's durability boundary by default after normal completion and when execution unwinds through an exception: accepted fragments are exported as collision-safe change files before the process activity window is released. Ctrl+C and process exit also attempt this cleanup. Use `--retain-window` only when the prior timeout-based activity behavior is explicitly required.

Each invocation uses `{MachineName}-pits-{PID}.flag`. Release succeeds only while the flag content still identifies that OS process, and writes an expired epoch timestamp in place rather than deleting/recreating the OneDrive-backed file. Another process's activity flag cannot be released.

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

- Coordinated release order: `OsLibCore -> RaiUtils -> RaiImage -> JsonPit -> ImgSeeder -> PitSeeder`

When a matching tag is pushed from the `RAIkeep` repository, the GitHub Actions workflow at `.github/workflows/publish-pitseeder-nuget.yml` now:

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
