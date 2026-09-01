using JsonPit;
using Newtonsoft.Json.Linq;
using OsLib;

namespace PitSeeder.Tests;

public sealed class PointInTimeExportTests : IDisposable
{
	private readonly RaiPath root =
		Os.TempDir / "RAIkeep" / "pitseeder-tests" / "cr017-point-in-time-export";
	private static readonly DateTimeOffset T1 =
		new(2026, 8, 27, 10, 0, 0, TimeSpan.Zero);
	private static readonly DateTimeOffset T2 = T1.AddHours(1);
	private static readonly DateTimeOffset T3 = T2.AddHours(1);

	public PointInTimeExportTests()
	{
		Cleanup();
		root.mkdir();
	}

	public void Dispose() => Cleanup();

	[Fact]
	public void SinglePitJson_AtCutoff_UsesNativeHistoryAndEmitsEnvelope()
	{
		WriteHistoricalPit(
			"Person",
			Fragment("Known", T1, ("Name", "Alpha"), ("Country", "ZA")),
			Fragment("Known", T2, ("Name", "Beta")),
			Fragment("Known", T3, ("Name", "Gamma")),
			Fragment("Future", T3, ("Name", "Not yet")),
			Fragment("Deleted", T1, ("Name", "Temporary")),
			DeletedFragment("Deleted", T2));

		var beforeExport = DateTimeOffset.UtcNow;
		var run = RunPits(
			"export", "Person", "--json", "--at", T2.ToString("O"),
			"-r", root.FullPath, "-n");
		var afterExport = DateTimeOffset.UtcNow;

		Assert.Equal(0, run.exitCode);
		var document = JObject.Parse(run.output);
		Assert.Equal(T2, document["_export"]!["at"]!.ToObject<DateTimeOffset>());
		var exported = document["_export"]!["exported"]!.ToObject<DateTimeOffset>();
		Assert.InRange(exported, beforeExport, afterExport);
		var item = Assert.Single(Assert.IsType<JArray>(document["data"]));
		Assert.Equal("Known", item["Id"]?.Value<string>());
		Assert.Equal("Beta", item["Name"]?.Value<string>());
		Assert.Equal("ZA", item["Country"]?.Value<string>());
	}

	[Fact]
	public void SinglePitJson_AtCutoff_AppliesNestedTombstones()
	{
		WriteHistoricalPit(
			"Activity",
			Fragment(
				"Rehearsal",
				T1,
				("What", new JObject { ["Instrument"] = "Guitar", ["Chat"] = "Legacy" })),
			Fragment(
				"Rehearsal",
				T2,
				("What", new JObject { ["Chat"] = JValue.CreateNull() })));

		var run = RunPits(
			"export", "Activity", "--json", "--at", T2.ToString("O"),
			"-r", root.FullPath, "-n");

		Assert.Equal(0, run.exitCode);
		var item = Assert.Single((JArray)JObject.Parse(run.output)["data"]!);
		var what = Assert.IsType<JObject>(item["What"]);
		Assert.Equal("Guitar", what["Instrument"]?.Value<string>());
		Assert.False(what.ContainsKey("Chat"));
	}

	[Fact]
	public void SameCutoff_ReflectsBackdatedHistoryAvailableToLaterInvocation()
	{
		WriteHistoricalPit("Person", Fragment("Known", T1, ("Name", "Alpha")));

		var first = ExportData("Person", T2);
		Assert.Equal("Alpha", Assert.Single(first)["Name"]?.Value<string>());

		AppendHistorical("Person", Fragment("Known", T1.AddMinutes(30), ("Name", "Beta")));

		var second = ExportData("Person", T2);
		Assert.Equal("Beta", Assert.Single(second)["Name"]?.Value<string>());
	}

	[Fact]
	public void SinglePitJson_DeletionWallAndResurrection_FollowRequestedCutoff()
	{
		WriteHistoricalPit(
			"Person",
			Fragment("Returning", T1, ("Name", "Before deletion"), ("Legacy", "old")),
			DeletedFragment("Returning", T2),
			Fragment("Returning", T3, ("Name", "After resurrection")));

		Assert.Empty(ExportData("Person", T2));
		var resurrected = Assert.Single(ExportData("Person", T3));
		Assert.Equal("After resurrection", resurrected["Name"]?.Value<string>());
		Assert.Null(resurrected["Legacy"]);
	}

	[Fact]
	public void SinglePitFile_AtCutoff_WritesSameEnvelopeAndKeepsEstablishedFilename()
	{
		WriteHistoricalPit("Person", Fragment("Known", T1, ("Name", "Alpha")));
		var historicalDirectory = root / "historical-export";
		var currentDirectory = root / "current-export";

		var historical = RunPits(
			"export", "Person", "--out-dir", historicalDirectory.FullPath,
			"--at", T2.ToString("O"), "-r", root.FullPath, "-n");

		Assert.Equal(0, historical.exitCode);
		var historicalFile = new TextFile(historicalDirectory, "Person", "json");
		Assert.True(historicalFile.Exists());
		var document = JObject.Parse(historicalFile.ReadAllText());
		Assert.NotNull(document["_export"]);
		Assert.IsType<JArray>(document["data"]);

		var current = RunPits(
			"export", "Person", "--out-dir", currentDirectory.FullPath,
			"-r", root.FullPath, "-n");
		Assert.Equal(0, current.exitCode);
		var currentFile = new TextFile(currentDirectory, "Person", "json");
		Assert.IsType<JArray>(JToken.Parse(currentFile.ReadAllText()));
	}

	[Fact]
	public void WwwaJson_AtCutoff_ProjectsAllPitsAndPreservesMissingReference()
	{
		WriteHistoricalPit(
			"Object",
			Fragment("Existing", T1, ("Name", "Available at cutoff")),
			Fragment("Future", T3, ("Name", "Created later")));
		WriteHistoricalPit(
			"Activity",
			Fragment(
				"ScheduleRehearsal",
				T1,
				("What", new JObject
				{
					["ExistingRef"] = "Existing",
					["MissingRef"] = "Future"
				})));
		WriteHistoricalPit("Person", Fragment("FuturePerson", T3, ("Name", "Later")));
		WriteHistoricalPit("Place", Fragment("FuturePlace", T3, ("Name", "Later")));

		var run = RunPits(
			"export", "--wwwa", "--json", "--at", T2.ToString("O"),
			"-r", root.FullPath, "-n");

		Assert.Equal(0, run.exitCode);
		var document = JObject.Parse(run.output);
		var data = Assert.IsType<JObject>(document["data"]);
		Assert.Empty(Assert.IsType<JArray>(data["Person"]));
		Assert.Empty(Assert.IsType<JArray>(data["Place"]));
		Assert.Equal("Existing", Assert.Single(Assert.IsType<JArray>(data["Object"]))["Id"]?.Value<string>());
		var activity = Assert.Single(Assert.IsType<JArray>(data["Activity"]));
		Assert.Equal(
			"Available at cutoff",
			activity["ExistingRef"]?["Name"]?.Value<string>());
		var unresolved = Assert.IsType<JObject>(activity["What"]);
		Assert.Equal("Future", unresolved["MissingRef"]?.Value<string>());

		var exportDirectory = root / "wwwa-historical-export";
		var fileRun = RunPits(
			"export", "--wwwa", "--out-dir", exportDirectory.FullPath,
			"--at", T2.ToString("O"), "-r", root.FullPath, "-n");
		Assert.Equal(0, fileRun.exitCode);
		var fileDocument = JObject.Parse(
			new TextFile(exportDirectory, "wwwa", "json").ReadAllText());
		Assert.True(JToken.DeepEquals(document["data"], fileDocument["data"]));

		var currentRun = RunPits(
			"export", "--wwwa", "--json", "-r", root.FullPath, "-n");
		Assert.Equal(0, currentRun.exitCode);
		var current = JObject.Parse(currentRun.output);
		Assert.Null(current["_export"]);
		Assert.IsType<JArray>(current["Activity"]);
	}

	[Fact]
	public void TimestampWithNumericOffset_IsNormalizedToUtcInEnvelope()
	{
		WriteHistoricalPit("Person", Fragment("Known", T1, ("Name", "Alpha")));

		var run = RunPits(
			"export", "Person", "--json", "--at", "2026-08-27T13:00:00+02:00",
			"-r", root.FullPath, "-n");

		Assert.Equal(0, run.exitCode);
		Assert.Contains(
			"\"at\": \"2026-08-27T11:00:00.0000000Z\"",
			run.output,
			StringComparison.Ordinal);
	}

	[Theory]
	[InlineData("2026-08-27T12:00:00")]
	[InlineData("2026-08-27 12:00:00Z")]
	[InlineData("2026-08-27T12:00Z")]
	[InlineData("not-a-timestamp")]
	public void ParseProjectionTimestamp_RejectsAmbiguousOrMalformedValues(string value)
	{
		var exception = Assert.Throws<ArgumentException>(() => Program.ParseProjectionTimestamp(value));
		Assert.Contains("explicit Z or numeric offset", exception.Message);
	}

	[Fact]
	public void ExportCommand_MissingAtValue_FailsWithActionableError()
	{
		var run = RunPits(
			"export", "Person", "--json", "-r", root.FullPath, "-n", "--at");

		Assert.Equal(1, run.exitCode);
		Assert.Contains("--at", run.output);
		Assert.Contains("requires a value", run.output);
	}

	[Fact]
	public void LegacyExport_MissingAtValue_FailsWithActionableError()
	{
		WriteHistoricalPit("Person", Fragment("Known", T1, ("Name", "Alpha")));

		var run = RunPits(
			"Person", "--json", "-r", root.FullPath, "-n", "--at");

		Assert.Equal(1, run.exitCode);
		Assert.Contains("--at", run.output);
		Assert.Contains("requires a value", run.output);
	}

	[Fact]
	public void LegacySeed_AtOption_IsRejectedInsteadOfIgnored()
	{
		var source = new TextFile(root, "Person", "json5")
		{
			Lines = ["[{ Id: 'Known', Name: 'Alpha' }]"],
			Changed = true
		};
		source.Save();

		var run = RunPits(
			"-s", source.FullName, "Person", "-r", root.FullPath, "-n",
			"--at", T2.ToString("O"));

		Assert.Equal(1, run.exitCode);
		Assert.Contains("--at applies only to export", run.output);
	}

	private JArray ExportData(string pitName, DateTimeOffset at)
	{
		var run = RunPits(
			"export", pitName, "--json", "--at", at.ToString("O"),
			"-r", root.FullPath, "-n");
		Assert.Equal(0, run.exitCode);
		return Assert.IsType<JArray>(JObject.Parse(run.output)["data"]);
	}

	private void WriteHistoricalPit(string pitName, params PitItem[] fragments)
	{
		var pitDirectory = root / pitName;
		using var pit = new Pit(
			pitDirectory,
			subscriber: "cr017-test-writer",
			readOnly: false,
			unflagged: true,
			autoload: false);
		foreach (var fragment in fragments)
			Assert.True(pit.AddHistorical(fragment));
		pit.Save(force: true);
	}

	private void AppendHistorical(string pitName, PitItem fragment)
	{
		using var pit = new Pit(
			root / pitName,
			subscriber: "cr017-test-writer",
			readOnly: false,
			unflagged: true,
			autoload: true);
		Assert.True(pit.AddHistorical(fragment));
		pit.Save(force: true);
	}

	private static PitItem Fragment(
		string id,
		DateTimeOffset modified,
		params (string Name, object? Value)[] properties)
	{
		var fragment = new PitItem(id, invalidate: false, timestamp: modified);
		foreach (var (name, value) in properties)
			fragment[name] = value is JToken token ? token : JToken.FromObject(value!);
		return fragment;
	}

	private static PitItem DeletedFragment(string id, DateTimeOffset modified)
	{
		var fragment = new PitItem(id, invalidate: false, timestamp: modified)
		{
			Deleted = true
		};
		return fragment;
	}

	private void Cleanup()
	{
		try
		{
			if (root.Exists())
				new RaiFile(root.Path).rmdir(depth: 12, deleteFiles: true);
		}
		catch { }
	}

	private static (int exitCode, string output) RunPits(params string[] args)
	{
		var pitsDll = new RaiFile(new RaiPath(AppContext.BaseDirectory), "pits", "dll");
		Assert.True(pitsDll.Exists(), $"Expected pits.dll at {pitsDll.FullName}");
		var result = PitsCommand.ForManagedAssembly(pitsDll).Run(args);
		return (result.ExitCode, result.Output);
	}
}
