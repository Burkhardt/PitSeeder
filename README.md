# PitSeeder

PitSeeder (`pits`) is a .NET command-line tool for working with [JsonPit](https://github.com/Burkhardt/RAIkeep) data stores. It can seed pits from JSON/JSON5 source files, export pits to JSON, and produce resolved WWWA exports where foreign key references are expanded inline.

Within this repository, PitSeeder lives under `RAIkeep/PitSeeder` so it can build against the local `JsonPit` and `OsLib` sources before those packages are published.

## 3.13.0

- Coordinated minor release: aligns `PitSeeder` with `JsonPit 3.13.0` and `OsLibCore 3.13.0`.
- Keeps `PitSeeder` last in the coordinated release order, immediately after `ImgSeeder`/`iorg`.
- Current release notes: [RELEASE_NOTES_3.13.0.md](RELEASE_NOTES_3.13.0.md)

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

```
pits [options] [<pit name>]
```

| Option | Description |
|--------|-------------|
| `-h`, `--help` | Print all options with resolved paths |
| `-v`, `--version` | Print version info |
| `-n`, `--nologo` | Suppress the banner |
| `-b`, `--debug` | Enable debug output |
| `-r`, `--pitroot` | Root directory containing pits; when used with `-c`, this is relative to the configured cloud root |
| `-c`, `--cloud` | Cloud provider name (looks up root in `~/.config/RAIkeep.json5`) |
| `-s`, `--source` | Source file for import (JSON or JSON5) |
| `-e`, `--export` | Export directory for JSON output |
| `--json` | Export to stdout (for piping to `jq`, `grep`, etc.) |
| `--wwwa` | Operate on all 4 WWWA pits (Person, Object, Place, Activity) |
| `<pit name>` | Positional argument: the pit to operate on (e.g., `Person`) |

## Features

### Seed a single pit

Import a JSON5 file into a pit under the given pit root:

```bash
pits -s ./sample/Person.json5 -r ./output/
```

This creates `./output/Person/Person.pit`.

### Seed the WWWA set

Import all four WWWA files (Person, Object, Place, Activity) from a source directory:

```bash
pits -s ./sample/ -r ./output/ --wwwa
```

### Export a single pit to a file

```bash
pits -r /path/to/pitroot/ Person -e ~/export/
```

Writes `~/export/Person.json` containing the projected current state of all items.

### Export a single pit to stdout

```bash
pits -n -r /path/to/pitroot/ Person --json
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
pits -n -r /path/to/pitroot/ Person --json | jq '.[] | select(.Id == "Nomsa") | .Instruments'
```

```json
["Voice", "Percussion", "Dance"]
```

### Export all WWWA pits with resolved foreign keys

```bash
pits -r /path/to/pitroot/ --wwwa -e ~/export/
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
pits -n -r /path/to/pitroot/ --wwwa --json | jq '.Place[] | select((.Id | startswith("SD")) or (.Name | contains("Zoo"))) | {Id, Name}'
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
pits -c OneDrive -r LiveAfricaStage Person --json
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

See [BuildFromSource.md](BuildFromSource.md) for:

- building from source
- building inside the `RAIkeep` workspace against local projects
- packing and publishing the NuGet tool
- publishing self-contained binaries for macOS, Ubuntu, and Windows

## License

This project is licensed under the Apache 2.0 license. See [LICENSE](LICENSE).
