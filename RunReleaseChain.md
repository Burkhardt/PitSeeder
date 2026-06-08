# How to run next time:

<code>cd /Users/rsb/Project2026/GitHub/RAIkeep
scripts/release-chain.sh 3.9.1</code>

The enforced package order is:

- `OsLibCore`
- `RaiUtils`
- `RaiImage`
- `JsonPit`
- `ImgSeeder`
- `PitSeeder`

The chain keeps the 300-second wait between published packages and verifies package visibility through the NuGet flat-container `.nupkg` URL.

## About running inside the LLM:

```
Yes, this can be run within this chat workflow by asking me to execute it.
I am using GPT-5.3-Codex.
The exact model tier selection such as Medium is controlled by your Copilot/session configuration, not by the shell script itself.
