using System.Diagnostics;
using JsonPit;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;
using OsLib;

namespace PitSeeder.Tests;

/// <summary>
/// CR003 / recovery-concept scenario 28 — <c>pits --events</c> is a filtered,
/// deterministic, strictly read-only audit path: it opens no <see cref="Pit"/>, creates
/// no process or master flag, merges nothing, and writes no audit event.
/// </summary>
public sealed class EventsAuditModeTests : IDisposable
{
	private readonly RaiPath root;
	private readonly RaiPath pitDir;
	private const string PitName = "Person";

	public EventsAuditModeTests()
	{
		root = Os.TempDir / "RAIkeep" / "pitseeder-tests" / "events-audit";
		Cleanup();
		root.mkdir();
		pitDir = (root / PitName).mkdir();
		SeedEvents();
	}

	public void Dispose() => Cleanup();

	private void Cleanup()
	{
		try
		{
			if (root.Exists())
				new RaiFile(root.Path).rmdir(depth: 10, deleteFiles: true);
		}
		catch { }
	}

	private static JObject Event(string machine, string stage, LogLevel level, long ticks, string message) => new()
	{
		["SchemaVersion"] = "1",
		["EventId"] = Guid.NewGuid().ToString("D"),
		["UtcTime"] = new DateTimeOffset(ticks, TimeSpan.Zero).UtcDateTime.ToString("o"),
		["UtcTicks"] = ticks,
		["Level"] = level.ToString(),
		["Stage"] = stage,
		["Pit"] = PitName,
		["Machine"] = machine,
		["Process"] = $"{machine}-app-1",
		["Master"] = string.Empty,
		["Role"] = "Master",
		["FragmentCount"] = 0,
		["FileCount"] = 0,
		["CorrelationId"] = Guid.NewGuid().ToString("D"),
		["Operation"] = "Test",
		["Message"] = message,
		["Exception"] = string.Empty
	};

	private void SeedEvents()
	{
		var baseTicks = DateTimeOffset.UtcNow.AddMinutes(-30).UtcTicks;
		var local = Environment.MachineName;
		_ = new EventFile(pitDir, $"{baseTicks + 1}_{local}-app-1_RoleDetermined",
			Event(local, "RoleDetermined", LogLevel.Information, baseTicks + 1, "local info"));
		_ = new EventFile(pitDir, $"{baseTicks + 2}_{local}-app-1_Failed",
			Event(local, "Failed", LogLevel.Error, baseTicks + 2, "local error"));
		_ = new EventFile(pitDir, $"{baseTicks + 3}_Zebra-app-9_ConflictDetected",
			Event("Zebra", "ConflictDetected", LogLevel.Warning, baseTicks + 3, "zebra warning"));
		_ = new EventFile(pitDir, $"{baseTicks + 4}_Zebra-app-9_CleanupPending",
			Event("Zebra", "CleanupPending", LogLevel.Debug, baseTicks + 4, "zebra debug"));
	}

	private List<string> SnapshotPitDirectory() =>
		Directory.EnumerateFileSystemEntries(pitDir.Path, "*", SearchOption.AllDirectories)
			.Select(p => $"{p}|{(File.Exists(p) ? new FileInfo(p).Length : 0)}")
			.OrderBy(p => p, StringComparer.Ordinal)
			.ToList();

	[Fact]
	public void Events_DefaultFilters_EmitAllMachines_DeterministicallyOrdered_WithoutSideEffects()
	{
		var before = SnapshotPitDirectory();
		var run = RunPits("-n", "-r", root.FullPath, PitName, "--events");
		Assert.Equal(0, run.exitCode);

		// Deterministic human ordering: machine, UTC time, event identity.
		var lines = run.output.Split('\n', StringSplitOptions.RemoveEmptyEntries)
			.Where(l => l.Contains('\t')).ToList();
		Assert.Equal(4, lines.Count);
		var machines = lines.Select(l => l.Split('\t')[0]).ToList();
		Assert.Equal(machines.OrderBy(m => m, StringComparer.Ordinal).ToList(), machines);

		// Strictly read-only: no process flag, master flag, change file, or new event.
		Assert.Equal(before, SnapshotPitDirectory());
	}

	[Fact]
	public void Events_JsonOutput_EmitsFilteredEventsAsParseableJson()
	{
		var run = RunPits("-n", "-r", root.FullPath, PitName, "--events", "--json", "--event-level", "warning");
		Assert.Equal(0, run.exitCode);
		var jsonStart = run.output.IndexOf('[');
		Assert.True(jsonStart >= 0, $"Expected a JSON array in output: {run.output}");
		var parsed = JArray.Parse(run.output[jsonStart..]);
		// Inclusive minimum severity: Warning includes Warning and Error, excludes Debug/Information.
		Assert.Equal(2, parsed.Count);
		Assert.All(parsed, e => Assert.Contains((string?)e["Level"], new[] { "Warning", "Error" }));
	}

	[Fact]
	public void Events_MachineFilters_SupportAllLocalAndNamedMachine()
	{
		var local = RunPits("-n", "-r", root.FullPath, PitName, "--events", "--event-machine", "local", "--json");
		Assert.Equal(0, local.exitCode);
		var localEvents = JArray.Parse(local.output[local.output.IndexOf('[')..]);
		Assert.Equal(2, localEvents.Count);
		Assert.All(localEvents, e => Assert.Equal(Environment.MachineName, (string?)e["Machine"]));

		var named = RunPits("-n", "-r", root.FullPath, PitName, "--events", "--event-machine", "Zebra", "--json");
		Assert.Equal(0, named.exitCode);
		var zebraEvents = JArray.Parse(named.output[named.output.IndexOf('[')..]);
		Assert.Equal(2, zebraEvents.Count);
		Assert.All(zebraEvents, e => Assert.Equal("Zebra", (string?)e["Machine"]));

		var all = RunPits("-n", "-r", root.FullPath, PitName, "--events", "--event-machine", "all", "--json");
		Assert.Equal(0, all.exitCode);
		Assert.Equal(4, JArray.Parse(all.output[all.output.IndexOf('[')..]).Count);
	}

	[Fact]
	public void Events_LevelFilter_IsCaseInsensitive_InvalidLevelFailsWithoutSideEffects()
	{
		var mixedCase = RunPits("-n", "-r", root.FullPath, PitName, "--events", "--event-level", "ERROR", "--json");
		Assert.Equal(0, mixedCase.exitCode);
		Assert.Single(JArray.Parse(mixedCase.output[mixedCase.output.IndexOf('[')..]));

		var before = SnapshotPitDirectory();
		var invalid = RunPits("-n", "-r", root.FullPath, PitName, "--events", "--event-level", "loud");
		Assert.Equal(1, invalid.exitCode);
		Assert.Contains("event-level", invalid.output, StringComparison.OrdinalIgnoreCase);
		Assert.Equal(before, SnapshotPitDirectory());
	}

	[Fact]
	public void Events_RequiresPositionalPitName_AndRejectsWwwaCombination()
	{
		var before = SnapshotPitDirectory();
		var missingName = RunPits("-n", "-r", root.FullPath, "--events");
		Assert.Equal(1, missingName.exitCode);

		var wwwa = RunPits("-n", "-r", root.FullPath, PitName, "--events", "--wwwa");
		Assert.Equal(1, wwwa.exitCode);
		Assert.Contains("wwwa", wwwa.output, StringComparison.OrdinalIgnoreCase);
		Assert.Equal(before, SnapshotPitDirectory());
	}

	[Fact]
	public void Events_MissingEventsDirectory_SucceedsEmpty_WithoutCreatingIt()
	{
		var emptyPit = (root / "Object").mkdir();
		var run = RunPits("-n", "-r", root.FullPath, "Object", "--events");
		Assert.Equal(0, run.exitCode);
		Assert.False(Directory.Exists((emptyPit / EventDirectory.Name).Path),
			"A read-only audit must never create the Events directory.");
	}

	private static (int exitCode, string output) RunPits(params string[] args)
	{
		var pitsDll = new RaiFile(new RaiPath(AppContext.BaseDirectory), "pits", "dll");
		Assert.True(pitsDll.Exists(), $"Expected pits.dll at {pitsDll.FullName}");

		var startInfo = new ProcessStartInfo("dotnet")
		{
			RedirectStandardOutput = true,
			RedirectStandardError = true,
			UseShellExecute = false
		};
		startInfo.ArgumentList.Add(pitsDll.FullName);
		foreach (var arg in args)
			startInfo.ArgumentList.Add(arg);

		using var process = Process.Start(startInfo)!;
		var stdout = process.StandardOutput.ReadToEnd();
		var stderr = process.StandardError.ReadToEnd();
		process.WaitForExit();
		return (process.ExitCode, stdout + stderr);
	}
}
