# Build From Source

This document describes how to build and publish `pits` from source as a self-contained command-line binary for macOS, Ubuntu, and Windows.

## Prerequisites

- .NET SDK 10.x installed
- Git installed
- Access to the package source used by the project, normally `https://api.nuget.org/v3/index.json`

Clone the repository and move into the root directory:

```bash
git clone <repo-url>
cd PitSeeder
```

## Project Layout

The executable project is:

```text
pits/pits.csproj
```

All publish commands below should be run from the repository root.

## Restore And Build

Restore packages:

```bash
dotnet restore pits/pits.csproj
```

Build the project:

```bash
dotnet build pits/pits.csproj -c Release
```

If restore or publish fails because a package version is not yet available on NuGet, re-run restore after the package becomes visible or temporarily use the last confirmed published version.

## Install As A .NET Tool From NuGet

This project can also be packaged as a .NET tool, which is the easiest installation path for users who already have the .NET runtime or SDK installed.

After the package is published to NuGet, users can install it with:

```bash
dotnet tool install --global PitSeeder
```

On macOS and Linux, the most practical option is often to install the tool into a standard directory that is already on `PATH`, such as `/usr/local/bin`:

```bash
sudo dotnet tool install PitSeeder --tool-path /usr/local/bin
```

Then run:

```bash
pits --help
```

Update an existing installation with:

```bash
dotnet tool update --global PitSeeder
```

Uninstall with:

```bash
dotnet tool uninstall --global PitSeeder
```

For a `/usr/local/bin` installation, use:

```bash
sudo dotnet tool update PitSeeder --tool-path /usr/local/bin
sudo dotnet tool uninstall PitSeeder --tool-path /usr/local/bin
```

Important tradeoff:

- A .NET tool is convenient to install from NuGet.
- A .NET tool is usually framework-dependent, not self-contained.
- Users who do not want any .NET dependency should use the self-contained binaries described later in this document.
- Installing with `--tool-path /usr/local/bin` avoids adding `~/.dotnet/tools` to `PATH`.

## Pack And Publish The NuGet Tool

The project is configured so that `dotnet pack` creates a NuGet package for the `pits` command.

Create the package:

```bash
dotnet pack pits/pits.csproj -c Release
```

The resulting `.nupkg` file is written to:

```text
artifacts/nuget/
```

Before publishing to NuGet, test the package locally.

Create a temporary local tool feed from the generated package directory:

```bash
dotnet tool install --global PitSeeder --add-source ./artifacts/nuget
```

If an older version is already installed, use:

```bash
dotnet tool update --global PitSeeder --add-source ./artifacts/nuget
```

Once the package is verified, publish it to NuGet:

```bash
dotnet nuget push artifacts/nuget/*.nupkg --source https://api.nuget.org/v3/index.json --api-key <your-api-key>
```

After the package becomes available on NuGet, users can install it without the local source flag:

```bash
dotnet tool install --global PitSeeder
```

## Publish Strategy

For a simple CLI deployment, the most practical output is:

- `SelfContained=true`
- `PublishSingleFile=true`
- `-c Release`

That produces one main executable plus, depending on settings, a `.pdb` file for debugging.

Typical output folder pattern:

```text
pits/bin/Release/net10.0/<rid>/publish/
```

Common runtime identifiers:

- macOS Apple Silicon: `osx-arm64`
- macOS Intel: `osx-x64`
- Ubuntu x64: `linux-x64`
- Ubuntu ARM64: `linux-arm64`
- Windows x64: `win-x64`

## macOS

### Publish

For Apple Silicon:

```bash
dotnet publish pits/pits.csproj -c Release -r osx-arm64 --self-contained true /p:PublishSingleFile=true
```

For Intel Macs:

```bash
dotnet publish pits/pits.csproj -c Release -r osx-x64 --self-contained true /p:PublishSingleFile=true
```

### Deploy To A Standard CLI Location

For macOS, `/usr/local/bin` is a reasonable install location because it is commonly on `PATH`.

This project already contains an MSBuild target that can automatically copy the published `pits` binary and its `pits.pdb` file to `/usr/local/bin`, but only for:

- `RuntimeIdentifier=osx-arm64`
- `SelfContained=true`
- `PublishSingleFile=true`
- `/p:DeployToUsrLocalBin=true`

Use:

```bash
dotnet publish pits/pits.csproj -c Release -r osx-arm64 --self-contained true /p:PublishSingleFile=true /p:DeployToUsrLocalBin=true
```

If you prefer to deploy manually:

```bash
sudo cp pits/bin/Release/net10.0/osx-arm64/publish/pits /usr/local/bin/
sudo cp pits/bin/Release/net10.0/osx-arm64/publish/pits.pdb /usr/local/bin/
```

Verify:

```bash
which pits
pits --help
```

## Ubuntu

### Publish

For Ubuntu on x64:

```bash
dotnet publish pits/pits.csproj -c Release -r linux-x64 --self-contained true /p:PublishSingleFile=true
```

For Ubuntu on ARM64:

```bash
dotnet publish pits/pits.csproj -c Release -r linux-arm64 --self-contained true /p:PublishSingleFile=true
```

### Deploy To A Standard CLI Location

On Ubuntu, `/usr/local/bin` is also a reasonable system-wide install location.

This repository does not currently contain an automatic Linux deploy target, so copy the files manually:

```bash
sudo cp pits/bin/Release/net10.0/linux-x64/publish/pits /usr/local/bin/
sudo cp pits/bin/Release/net10.0/linux-x64/publish/pits.pdb /usr/local/bin/
sudo chmod 755 /usr/local/bin/pits
```

Verify:

```bash
which pits
pits --help
```

## Windows

### Publish

For Windows x64:

```powershell
dotnet publish pits/pits.csproj -c Release -r win-x64 --self-contained true /p:PublishSingleFile=true
```

### Deploy To A Reasonable Directory

Windows does not use `/usr/local/bin`. A practical equivalent for a CLI tool is a dedicated directory such as:

```text
C:\Program Files\PitSeeder
```

Another common developer-oriented choice is:

```text
C:\Tools
```

If you want a machine-wide installation, `C:\Program Files\PitSeeder` is the safer default.

Create the target directory and copy the published files:

```powershell
New-Item -ItemType Directory -Force -Path "C:\Program Files\PitSeeder" | Out-Null
Copy-Item "pits\bin\Release\net10.0\win-x64\publish\pits.exe" "C:\Program Files\PitSeeder\"
Copy-Item "pits\bin\Release\net10.0\win-x64\publish\pits.pdb" "C:\Program Files\PitSeeder\"
```

Add that directory to `PATH` if it is not already there.

Verify:

```powershell
where.exe pits
pits --help
```

## Recommended Commands Summary

macOS Apple Silicon with automatic deploy:

```bash
dotnet publish pits/pits.csproj -c Release -r osx-arm64 --self-contained true /p:PublishSingleFile=true /p:DeployToUsrLocalBin=true
```

Ubuntu x64:

```bash
dotnet publish pits/pits.csproj -c Release -r linux-x64 --self-contained true /p:PublishSingleFile=true
```

Windows x64:

```powershell
dotnet publish pits/pits.csproj -c Release -r win-x64 --self-contained true /p:PublishSingleFile=true
```

## Notes

- A self-contained publish is platform-specific. You need one publish per target OS and CPU architecture.
- A macOS binary will not run on Ubuntu or Windows.
- An Ubuntu binary will not run on macOS or Windows.
- A Windows binary will not run on macOS or Ubuntu.
- If you want to support both Intel and Apple Silicon on macOS, publish both `osx-x64` and `osx-arm64`.
- The current automatic deploy target in the project is only implemented for macOS `osx-arm64`.