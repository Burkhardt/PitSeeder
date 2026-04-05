# PitSeeder 0.5.0

PitSeeder 0.5.0 is built and validated against the current local source versions of:

- OsLib 3.7.3
- JsonPit 3.7.3

Notes:

- this release adds support for building `pits` against a local `RAIkeep` checkout through project references
- this is important for the current release because the latest OsLib and JsonPit package versions are not yet available on NuGet
- local development, packaging, and publish flows should therefore use the source-reference option for now
