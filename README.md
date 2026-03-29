# PitSeeder

PitSeeder is a .NET command-line tool for seeding JsonPit data stores from JSON and JSON5 source files.

It supports both:

- installation as a `dotnet tool` from NuGet
- self-contained binary publishing for macOS, Linux, and Windows

## Features

- seed a single JsonPit from one JSON or JSON5 file
- seed a set of WWWA pits in one run: Person, Object, Place, and Activity
- install as a `dotnet tool`
- publish as self-contained binaries for macOS, Linux, and Windows

## Install

After the package is published to NuGet:

```bash
dotnet tool install --global PitSeeder
```

Run:

```bash
pits --help
```

This installation method requires a compatible .NET runtime or SDK on the target machine.

## Quick Usage

Seed a single pit:

```bash
pits -s ./pits/sample/Person.json5 -d ./output/Person.pit
```

Seed the WWWA set:

```bash
pits -s ./pits/sample -d ./output --wwwa
```

## Build And Publish

See [BuildFromSource.md](BuildFromSource.md) for:

- building from source
- packing and publishing the NuGet tool
- publishing self-contained binaries for macOS, Ubuntu, and Windows

## License

This project is licensed under the Apache 2.0 license. See [LICENSE](LICENSE).