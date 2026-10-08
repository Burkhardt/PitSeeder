using Newtonsoft.Json.Linq;
using JsonPit;
using OsLib;
using Microsoft.Extensions.Logging;

namespace PitSeeder.Tests;

public sealed class CliSubcommandTests : IDisposable
{
	private readonly RaiPath root = Os.TempDir / "RAIkeep" / "pitseeder-tests" / "cr006-subcommands" / Guid.NewGuid().ToString("N");

	public CliSubcommandTests()
	{
		Cleanup();
		root.mkdir();
	}

	public void Dispose() => Cleanup();

	[Fact]
	public void MissingConfigurationDiagnostic_DirectsOperatorToAmafu()
	{
		Assert.Equal(
			"RAIkeep configuration was not found at '~/.config/RAIkeep.json5'. " +
			"Run 'amafu init' to detect cloud providers and create it.",
			Program.MissingConfigurationDiagnostic());
	}

	[Fact]
	public void ListCommand_ListsPitsFromAnExplicitLocalDirectory()
	{
		CreatePitMarker("Activity");
		CreatePitMarker("Person");

		var run = RunPits("list", "-r", root.FullPath, "-n");

		Assert.Equal(0, run.exitCode);
		Assert.Contains($"Found 2 pit(s) in local directory '{root.FullPath}':", run.output);
		Assert.Contains("  Activity", run.output);
		Assert.Contains("  Person", run.output);
	}

	[Fact]
	public void Ls_IsAnExactAliasForList()
	{
		CreatePitMarker("Object");

		var run = RunPits("ls", "-r", root.FullPath, "-n");

		Assert.Equal(0, run.exitCode);
		Assert.Contains("  Object", run.output);
	}

	[Fact]
	public void GeneralHelp_ShowsWwwaPitStatusOnlyWhenExplicitOrContextual()
	{
		var weather = root / "Weather";
		weather.mkdir();

		var bareHelp = RunPits("--help", "-n");
		var weatherHelp = RunPits("--help", "-r", weather.FullPath, "-n");
		Assert.Equal(0, bareHelp.exitCode);
		Assert.Equal(0, weatherHelp.exitCode);
		Assert.DoesNotContain("Person.pit", bareHelp.output);
		Assert.DoesNotContain("Object.pit", bareHelp.output);
		Assert.DoesNotContain("Place.pit", bareHelp.output);
		Assert.DoesNotContain("Activity.pit", bareHelp.output);
		Assert.DoesNotContain("Person.pit", weatherHelp.output);

		CreatePit(root / "Person", "Person", "WWWA");
		Assert.True(Messages.HasWwwaPit(root));
		var contextualHelp = RunPits("--help", "-r", root.FullPath, "-n");
		var explicitHelp = RunPits("--help", "--wwwa", "-n");
		var personFile = new PitFile(root / "Person", "Person").FullName;

		Assert.Contains($"{Icons.Info} Person\t{Icons.Success}\t{personFile}", contextualHelp.output);
		Assert.Contains($"{Icons.Info} Activity\t{Icons.NotAvailable}", contextualHelp.output);
		Assert.Contains($"{Icons.Info} Person\t{Icons.NotAvailable}\tPerson.pit", explicitHelp.output);
		Assert.Contains($"{Icons.Info} Object\t{Icons.NotAvailable}\tObject.pit", explicitHelp.output);
		Assert.Contains($"{Icons.Info} Place\t\t{Icons.NotAvailable}\tPlace.pit", explicitHelp.output);
		Assert.Contains($"{Icons.Info} Activity\t{Icons.NotAvailable}\tActivity.pit", explicitHelp.output);
	}

	[Fact]
	public void LsLa_BundlesLongAndAllFlagsForAListingRoot()
	{
		var tenant = root / "AIA";
		tenant.mkdir();
		var pitDirectory = tenant / "Person";
		CreatePit(pitDirectory, "Person", new string('x', 2048));

		var run = RunPits("ls", "-la", "-r", tenant.FullPath, "-n");

		Assert.Equal(0, run.exitCode);
		Assert.Contains("  Person", run.output);
		Assert.Contains("KB", run.output);
		Assert.Matches(@"\d{4}-\d{2}-\d{2} \d{2}:\d{2}", run.output);
		Assert.Equal(["-l", "-a"], Program.ExpandListOptionBundles(["-la"]));
		Assert.Equal(["-l", "-a"], Program.ExpandListOptionBundles(["-al"]));
	}

	[Fact]
	public void ListDiscovery_DeclaresCloudAndResolvedDirectory()
	{
		var output = Program.RenderPitDiscovery("GoogleDriveRainer", "/cloud/GoogleDriveRainer/AIA",
			["Activity", "Object", "Person", "Place"], all: false);

		Assert.Equal(
			"Found 4 pit(s) in cloud 'GoogleDriveRainer' (/cloud/GoogleDriveRainer/AIA):" + Environment.NewLine +
			"  Activity" + Environment.NewLine + "  Object" + Environment.NewLine +
			"  Person" + Environment.NewLine + "  Place",
			output);
	}

	[Fact]
	public void ListAllDiscovery_ReportsEveryCloudIncludingEmptyRoots()
	{
		var found = Program.RenderPitDiscovery("OneDrive", "/cloud/OneDrive/AIA", ["Activity"], all: true);
		var empty = Program.RenderPitDiscovery("ICloudDrive", "/cloud/ICloudDrive/AIA", [], all: true);

		Assert.Equal("[OneDrive] (/cloud/OneDrive/AIA): 1 pit(s) found" + Environment.NewLine + "  Activity", found);
		Assert.Equal("[ICloudDrive] (/cloud/ICloudDrive/AIA): No pits found.", empty);
	}

	[Fact]
	public void Seed_RejectsBareTenantWithoutAnExplicitCloud()
	{
		var source = new TextFile(root, "person", "json5") { Lines = ["[{ Id: 'Person1' }]"], Changed = true };
		source.Save();

		var run = RunPits("seed", "Person", "--source", source.FullName, "-r", "AIA", "-n");

		Assert.Equal(1, run.exitCode);
		Assert.Contains("refuses to guess a cloud", run.output + run.error);
	}

	[Fact]
	public void SeedAndExportCommands_RouteThroughWorkingHandlers()
	{
		var source = new TextFile(root, "people", "json5")
		{
			Lines = ["[{ Id: 'CommandPerson', Name: 'Command Mode' }]"],
			Changed = true
		};
		source.Save();

		var seed = RunPits("seed", "Person", "--source", source.FullName, "-r", root.FullPath, "-n");
		Assert.Equal(0, seed.exitCode);
		Assert.True(new RaiFile(root / "Person", "Person", "pit").Exists());

		var export = RunPits("export", "Person", "--json", "-r", root.FullPath, "-n");
		Assert.Equal(0, export.exitCode);
		var payload = JArray.Parse(export.output[export.output.IndexOf('[')..]);
		Assert.Equal("CommandPerson", (string?)Assert.Single(payload)["Id"]);
	}

	[Fact]
	public void SeedCommand_AcceptsSingleRootEntityWithNonEmptyStringId()
	{
		var source = new TextFile(root, "single-entity", "json5")
		{
			Lines = ["{ Id: 'PerformLive', Kind: 'UC', Name: 'Perform Live Show' }"],
			Changed = true
		};
		source.Save();

		var seed = RunPits("seed", "Activity", "--source", source.FullName, "-r", root.FullPath, "-n");
		var export = RunPits("export", "Activity", "--json", "-r", root.FullPath, "-n");

		Assert.Equal(0, seed.exitCode);
		Assert.Equal(0, export.exitCode);
		var item = Assert.Single(JArray.Parse(export.output[export.output.IndexOf('[')..]));
		Assert.Equal("PerformLive", item["Id"]?.Value<string>());
		Assert.Equal("UC", item["Kind"]?.Value<string>());
	}

	[Fact]
	public void SeedCommand_AcceptsKeyedEntityMap()
	{
		var source = new TextFile(root, "keyed-map", "json5")
		{
			Lines = ["{ Item1: { Id: 'Item1', Kind: 'Obj' }, Item2: { Id: 'Item2', Kind: 'Obj' } }"],
			Changed = true
		};
		source.Save();

		var seed = RunPits("seed", "Object", "--source", source.FullName, "-r", root.FullPath, "-n");
		var export = RunPits("export", "Object", "--json", "-r", root.FullPath, "-n");

		Assert.Equal(0, seed.exitCode);
		Assert.Equal(0, export.exitCode);
		var ids = JArray.Parse(export.output[export.output.IndexOf('[')..])
			.Select(item => item["Id"]?.Value<string>())
			.OfType<string>()
			.OrderBy(id => id)
			.ToArray();
		Assert.Equal(new[] { "Item1", "Item2" }, ids);
	}

	[Theory]
	[InlineData("{ Kind: 'UC', Name: 'Missing Id' }")]
	[InlineData("{ Id: '', Kind: 'UC' }")]
	[InlineData("{ Id: '   ', Kind: 'UC' }")]
	[InlineData("{ Id: 42, Kind: 'UC' }")]
	[InlineData("{ id: 'lowercase-is-not-Id', Kind: 'UC' }")]
	public void SeedCommand_RejectsInvalidSingleRootObjectWithThreeWayDiagnostic(string payload)
	{
		var source = new TextFile(root, "invalid-single", "json5")
		{
			Lines = [payload],
			Changed = true
		};
		source.Save();

		var seed = RunPits("seed", "Activity", "--source", source.FullName, "-r", root.FullPath, "-n");

		Assert.Equal(1, seed.exitCode);
		Assert.Contains("JSON array of entities", seed.output + seed.error);
		Assert.Contains("single entity object with a non-empty 'Id'", seed.output + seed.error);
		Assert.Contains("keyed map of entity objects", seed.output + seed.error);
		Assert.False((root / "Activity").Exists());
	}

	[Theory]
	[InlineData("[{ Name: 'Missing Id' }]")]
	[InlineData("{ Item1: { Name: 'Missing Id' } }")]
	public void SeedCommand_RejectsEntityWithoutIdBeforeOpeningPit(string payload)
	{
		var source = new TextFile(root, "missing-entity-id", "json5")
		{
			Lines = [payload],
			Changed = true
		};
		source.Save();

		var seed = RunPits("seed", "Activity", "--source", source.FullName, "-r", root.FullPath, "-n");

		Assert.Equal(1, seed.exitCode);
		Assert.Contains("entity without a non-empty string 'Id'", seed.output + seed.error);
		Assert.False((root / "Activity").Exists());
	}

	[Fact]
	public void SeedCommand_RejectsNonObjectArrayEntryBeforeOpeningPit()
	{
		var source = new TextFile(root, "invalid-array", "json5")
		{
			Lines = ["[{ Id: 'Valid' }, 'InvalidString']"],
			Changed = true
		};
		source.Save();

		var seed = RunPits("seed", "Activity", "--source", source.FullName, "-r", root.FullPath, "-n");

		Assert.Equal(1, seed.exitCode);
		Assert.Contains("may contain only JSON objects", seed.output + seed.error);
		Assert.False((root / "Activity").Exists());
	}

	[Theory]
	[InlineData("// Seed maintained by Zébio\n[{ Id: 'LeadingComment', Name: 'Line Comment' }]")]
	[InlineData("/* Seed maintained by Vesco. */\n[{ Id: 'LeadingComment', Name: 'Block Comment' }]")]
	public void SeedCommand_AcceptsLeadingJson5CommentBeforeArray(string payload)
	{
		var source = new TextFile(root, "leading-comment", "json5")
		{
			Lines = payload.Split('\n').ToList(),
			Changed = true
		};
		source.Save();

		var seed = RunPits("seed", "Person", "--source", source.FullName, "-r", root.FullPath, "-n");
		var export = RunPits("export", "Person", "--json", "-r", root.FullPath, "-n");

		Assert.Equal(0, seed.exitCode);
		Assert.Equal(0, export.exitCode);
		var payloadArray = JArray.Parse(export.output[export.output.IndexOf('[')..]);
		Assert.Equal("LeadingComment", (string?)Assert.Single(payloadArray)["Id"]);
	}

	[Fact]
	public void SeedCommand_DoesNotMaskMalformedJsonAfterLeadingComment()
	{
		var source = new TextFile(root, "malformed-leading-comment", "json5")
		{
			Lines = ["// A valid comment is not permission to ignore an invalid payload.", "[{ Id: ]"],
			Changed = true
		};
		source.Save();

		var seed = RunPits("seed", "Person", "--source", source.FullName, "-r", root.FullPath, "-n");

		Assert.NotEqual(0, seed.exitCode);
		Assert.False(new RaiFile(root / "Person", "Person", "pit").Exists());
	}

	[Theory]
	[InlineData("Modified", "'2026-09-26T20:00:00Z'")]
	[InlineData("deleted", "true")]
	public void SeedCommand_RejectsProtectedLifecycleAttributesBeforeOpeningPit(string attribute, string value)
	{
		var source = new TextFile(root, "protected-seed", "json5")
		{
			Lines = [$"[{{ Id: 'Protected', Role: 'Musician', {attribute}: {value} }}]"],
			Changed = true
		};
		source.Save();

		var seed = RunPits("seed", "Person", "--source", source.FullName, "-r", root.FullPath, "-n");

		Assert.Equal(1, seed.exitCode);
		Assert.Contains("protected attribute", seed.error, StringComparison.OrdinalIgnoreCase);
		Assert.Contains(attribute, seed.error, StringComparison.OrdinalIgnoreCase);
		Assert.False((root / "Person").Exists());
	}

	[Fact]
	public void DeletePropertyCommand_DeletesNestedProperty_AndPreservesSibling()
	{
		var source = new TextFile(root, "activities", "json5")
		{
			Lines = ["[{ Id: 'DeleteNested', What: { Instrument: 'Guitar', Chat: 'Legacy' } }]"],
			Changed = true
		};
		source.Save();
		Assert.Equal(0, RunPits("seed", "Activity", "--source", source.FullName, "-r", root.FullPath, "-n").exitCode);

		var deletion = RunPits(
			"delete-property", "Activity", "DeleteNested", "What.Chat",
			"-r", root.FullPath, "-n");
		Assert.Equal(0, deletion.exitCode);

		var export = RunPits("export", "Activity", "--json", "-r", root.FullPath, "-n");
		Assert.Equal(0, export.exitCode);
		var item = Assert.Single(JArray.Parse(export.output[export.output.IndexOf('[')..]));
		var what = Assert.IsType<JObject>(item["What"]);
		Assert.Equal("Guitar", what["Instrument"]?.Value<string>());
		Assert.False(what.ContainsKey("Chat"));
	}

	[Fact]
	public void DeleteItemCommand_RemovesItemFromProjectedExport()
	{
		var source = new TextFile(root, "objects", "json5")
		{
			Lines = ["[{ Id: 'Keep' }, { Id: 'LegacyRecord' }]"],
			Changed = true
		};
		source.Save();
		Assert.Equal(0, RunPits("seed", "Object", "--source", source.FullName, "-r", root.FullPath, "-n").exitCode);

		var deletion = RunPits("delete-item", "Object", "LegacyRecord", "-r", root.FullPath, "-n");
		Assert.Equal(0, deletion.exitCode);

		var export = RunPits("export", "Object", "--json", "-r", root.FullPath, "-n");
		Assert.Equal(0, export.exitCode);
		var items = JArray.Parse(export.output[export.output.IndexOf('[')..]);
		Assert.Equal("Keep", (string?)Assert.Single(items)["Id"]);
	}

	[Fact]
	public void DeleteCommands_RejectMalformedOrMissingTargets()
	{
		Assert.Equal(1, RunPits("delete-property", "Activity", "Item", "What..Chat", "-r", root.FullPath, "-n").exitCode);
		Assert.Equal(1, RunPits("delete-property", "Activity", "Missing", "What.Chat", "-r", root.FullPath, "-n").exitCode);
		Assert.Equal(1, RunPits("delete-item", "Activity", "Missing", "-r", root.FullPath, "-n").exitCode);
	}

	[Theory]
	[InlineData("Id")]
	[InlineData("modified")]
	[InlineData("Deleted")]
	public void DeletePropertyCommand_RejectsProtectedAttributes_WithoutChangingItem(string propertyPath)
	{
		var source = new TextFile(root, "protected-delete", "json5")
		{
			Lines = ["[{ Id: 'Protected', Role: 'Musician' }]"],
			Changed = true
		};
		source.Save();
		Assert.Equal(0, RunPits("seed", "Person", "--source", source.FullName, "-r", root.FullPath, "-n").exitCode);

		var deletion = RunPits(
			"delete-property", "Person", "Protected", propertyPath,
			"-r", root.FullPath, "-n");

		Assert.Equal(1, deletion.exitCode);
		Assert.Contains("Cannot tombstone protected attribute", deletion.error);
		var export = RunPits("export", "Person", "--json", "-r", root.FullPath, "-n");
		var item = Assert.Single(JArray.Parse(export.output[export.output.IndexOf('[')..]));
		Assert.Equal("Musician", item["Role"]?.Value<string>());
	}

	[Fact]
	public void Maintain_ReportOnlyThenApply_UsesDurableReceiptWithoutRepublishingChanges()
	{
		var pitPath = (root / "Activity").mkdir();
		RaiFile change;
		using (var pit = new JsonPit.Pit(pitPath, readOnly: false, unflagged: true, autoload: false))
		{
			pit.Add(new JsonPit.PitItem("Canonical"));
			pit.Save(force: true);
			change = pit.CreateChangeFile(new JsonPit.PitItem("Remote"), "RemotePeer-app-4242");
			pit.Maintain(apply: true);
		}
		var receiptPath = JsonPit.ReceiptFile.PathFor(change.FullName);
		var oldReceipt = new TextFile(receiptPath.FullName)
		{
			Lines = [DateTimeOffset.UtcNow.Subtract(TimeSpan.FromHours(1)).UtcDateTime.ToString("o")],
			Changed = true
		};
		oldReceipt.Save();

		var report = RunPits("maintain", "Activity", "--json", "-r", root.FullPath);
		Assert.Equal(0, report.exitCode);
		var reportJson = JObject.Parse(report.output);
		Assert.True((int?)reportJson["ChangeFilesEligible"] >= 1);
		Assert.True(change.Exists());
		Assert.True(receiptPath.Exists());

		var apply = RunPits("maintain", "Activity", "--apply", "--json", "-r", root.FullPath);
		Assert.Equal(0, apply.exitCode);
		var applyJson = JObject.Parse(apply.output);
		Assert.Equal(1, (int?)applyJson["ChangeFilesRemoved"]);
		Assert.False(change.Exists());
		Assert.False(receiptPath.Exists());
		Assert.Empty(pitPath.EnumerateFiles("*.json"));
	}

	[Fact]
	public void Maintain_ArchiveEvents_PreviewsThenApplies_AndAuditReadsTheArchive()
	{
		var pitPath = (root / "Activity").mkdir();
		using (var pit = new JsonPit.Pit(pitPath, readOnly: false, unflagged: true, autoload: false))
		{
			pit.Add(new JsonPit.PitItem("Canonical"));
			pit.Save(force: true);
		}
		var utc = new DateTimeOffset(2026, 9, 10, 14, 35, 0, TimeSpan.Zero);
		var loose = WriteAuditEvent(pitPath, "Activity", utc, "archivable event");

		var preview = RunPits(
			"maintain", "Activity", "--archive-events", "--json", "-r", root.FullPath, "-n");
		Assert.Equal(0, preview.exitCode);
		var previewJson = JObject.Parse(preview.output);
		Assert.False((bool)previewJson["Applied"]!);
		Assert.Equal(1, (int?)previewJson["EventFilesEligible"]);
		Assert.Equal("Events_20260910-1435_to_20260910-1435.zip", (string?)previewJson["EventArchiveName"]);
		Assert.True(loose.Exists());
		Assert.Empty((pitPath / EventDirectory.Name).EnumerateFiles("*.zip"));

		var apply = RunPits(
			"maintain", "Activity", "--archive-events", "--apply", "--json", "-r", root.FullPath, "-n");
		Assert.Equal(0, apply.exitCode);
		var applyJson = JObject.Parse(apply.output);
		Assert.Equal(1, (int?)applyJson["EventArchivesCreated"]);
		Assert.Equal(1, (int?)applyJson["EventFilesRemoved"]);
		Assert.False(loose.Exists());
		Assert.Single((pitPath / EventDirectory.Name).EnumerateFiles("*.zip"));

		var audit = RunPits("audit", "Activity", "--json", "-r", root.FullPath, "-n");
		Assert.Equal(0, audit.exitCode);
		var events = JArray.Parse(audit.output[audit.output.IndexOf('[')..]);
		Assert.Equal("archivable event", (string?)Assert.Single(events)["Message"]);
	}

	[Fact]
	public void Maintain_ArchiveEvents_WwwaArchivesEachExistingPitWithoutCreatingMissingPits()
	{
		foreach (var name in new[] { "Activity", "Object" })
		{
			var pitPath = (root / name).mkdir();
			using var pit = new JsonPit.Pit(pitPath, readOnly: false, unflagged: true, autoload: false);
			pit.Add(new JsonPit.PitItem($"{name}Item"));
			pit.Save(force: true);
			WriteAuditEvent(
				pitPath,
				name,
				new DateTimeOffset(2026, 9, 10, name == "Activity" ? 14 : 15, 0, 0, TimeSpan.Zero),
				$"{name} event");
		}

		var apply = RunPits(
			"maintain", "--wwwa", "--archive-events", "--apply", "--json", "-r", root.FullPath, "-n");

		Assert.Equal(0, apply.exitCode);
		var results = JArray.Parse(apply.output);
		Assert.Equal(4, results.Count);
		Assert.Equal(2, results.Count(result => (int?)result["EventArchivesCreated"] == 1));
		Assert.False((root / "Person").Exists());
		Assert.False((root / "Place").Exists());
		Assert.Single((root / "Activity" / EventDirectory.Name).EnumerateFiles("*.zip"));
		Assert.Single((root / "Object" / EventDirectory.Name).EnumerateFiles("*.zip"));

		var audit = RunPits("audit", "--wwwa", "--json", "-r", root.FullPath, "-n");
		Assert.Equal(0, audit.exitCode);
		Assert.Equal(2, JArray.Parse(audit.output[audit.output.IndexOf('[')..]).Count);
	}

	[Fact]
	public void Maintain_ValidatesExplicitDestructiveOptions_AndWwwaReportsFourPits()
	{
		Assert.Equal(1, RunPits(
			"maintain", "Activity", "--prune-process-flags", "--older-than", "7.00:00:00",
			"-r", root.FullPath, "-n").exitCode);
		Assert.Equal(1, RunPits(
			"maintain", "Activity", "--apply", "--older-than", "7.00:00:00",
			"-r", root.FullPath, "-n").exitCode);
		Assert.Equal(1, RunPits(
			"maintain", "Activity", "--repair-legacy-extensions",
			"-r", root.FullPath, "-n").exitCode);
		var activityPath = (root / "Activity").mkdir();
		using (var activity = new JsonPit.Pit(activityPath, readOnly: false, unflagged: true, autoload: false))
		{
			activity.Add(new JsonPit.PitItem("Existing"));
			activity.Save(force: true);
		}

		var wwwa = RunPits("maintain", "--wwwa", "--json", "-r", root.FullPath);
		Assert.Equal(0, wwwa.exitCode);
		var results = JArray.Parse(wwwa.output);
		Assert.Equal(4, results.Count);
		Assert.False((root / "Person").Exists());
		Assert.False((root / "Object").Exists());
		Assert.False((root / "Place").Exists());
		Assert.Contains(results, result =>
			result["Deferred"]?.Values<string>().Any(message =>
				message?.Contains("was not created", StringComparison.Ordinal) == true) == true);
	}

	[Fact]
	public void Maintain_WwwaWrongExistingRoot_FailsWithoutCreatingNestedPitDirectories()
	{
		var wrongRoot = (root / "Activity" / "AIA").mkdir();
		var parentWriteTime = Directory.GetLastWriteTimeUtc(wrongRoot.FullPath);

		var result = RunPits("maintain", "--wwwa", "--json", "-r", wrongRoot.FullPath, "-n");

		Assert.Equal(1, result.exitCode);
		Assert.Contains("contains none of the WWWA pits", result.output, StringComparison.Ordinal);
		Assert.Equal(parentWriteTime, Directory.GetLastWriteTimeUtc(wrongRoot.FullPath));
		foreach (var name in new[] { "Activity", "Person", "Object", "Place" })
			Assert.False((wrongRoot / name).Exists());
	}

	[Fact]
	public void Maintain_MissingRootAndSinglePit_FailWithoutCreatingEitherTarget()
	{
		var missingRoot = root / "missing-root";
		var missingPitRoot = (root / "single-root").mkdir();

		var missingRootResult = RunPits("maintain", "--wwwa", "--json", "-r", missingRoot.FullPath, "-n");
		var missingPitResult = RunPits("maintain", "Activity", "--json", "-r", missingPitRoot.FullPath, "-n");

		Assert.Equal(1, missingRootResult.exitCode);
		Assert.Equal(1, missingPitResult.exitCode);
		Assert.False(missingRoot.Exists());
		Assert.False((missingPitRoot / "Activity").Exists());
	}

	[Fact]
	public void LegacyExport_RemainsAvailableAlongsideCommandSyntax()
	{
		CreatePit();

		var legacy = RunPits("-n", "-r", root.FullPath, "Person", "--json");
		var command = RunPits("export", "Person", "--json", "-n", "-r", root.FullPath);

		Assert.Equal(0, legacy.exitCode);
		Assert.Equal(0, command.exitCode);
		Assert.Equal(
			JArray.Parse(legacy.output[legacy.output.IndexOf('[')..]).ToString(),
			JArray.Parse(command.output[command.output.IndexOf('[')..]).ToString());
	}

	[Fact]
	public void ExportCommand_RejectsAmbiguousTargetsAndOutputModes()
	{
		var targetConflict = RunPits("export", "Person", "--wwwa", "--json", "-r", root.FullPath, "-n");
		Assert.Equal(1, targetConflict.exitCode);
		Assert.Contains("either", targetConflict.output, StringComparison.OrdinalIgnoreCase);

		var outputConflict = RunPits("export", "--wwwa", "--json", "--out-dir", root.FullPath, "-r", root.FullPath, "-n");
		Assert.Equal(1, outputConflict.exitCode);
		Assert.Contains("exactly one output mode", outputConflict.output, StringComparison.OrdinalIgnoreCase);
	}

	[Fact]
	public void ContextualHelp_IsCommandScoped_AndBannerHasNoEqualsRule()
	{
		var auditHelp = RunPits("audit", "--help");
		Assert.Equal(0, auditHelp.exitCode);
		Assert.Contains("--machine", auditHelp.output);
		Assert.DoesNotContain("--source", auditHelp.output);
		var deleteHelp = RunPits("delete-property", "--help");
		Assert.Equal(0, deleteHelp.exitCode);
		Assert.Contains("<PropertyPath>", deleteHelp.output);
		Assert.Contains("What.Chat", deleteHelp.output);
		Assert.DoesNotContain("--source", deleteHelp.output);

		var rootHelp = RunPits("--help");
		Assert.Equal(0, rootHelp.exitCode);
		Assert.DoesNotContain("===", rootHelp.output, StringComparison.Ordinal);
		Assert.Contains("seed, export, audit, delete-property, delete-item, maintain", rootHelp.output, StringComparison.OrdinalIgnoreCase);
		Assert.DoesNotContain("(default)", rootHelp.output);
		Assert.DoesNotContain(" PitRoot", rootHelp.output);
		Assert.DoesNotContain("①", rootHelp.output, StringComparison.Ordinal);
		var cloudLine = Assert.Single(rootHelp.output.Split('\n', StringSplitOptions.RemoveEmptyEntries),
			line => line.StartsWith("-c, --cloud", StringComparison.Ordinal)).TrimEnd('\r');
		var firstProvider = Assert.Single(Messages.CloudProviderOptions().Take(1));
		var providerIcon = firstProvider.ToLowerInvariant() switch
		{
			"dropbox" => Icons.DropboxBoxOutline,
			"googledrive" => Icons.GoogleDriveBoxOutline,
			"iclouddrive" => Icons.ICloudDriveBoxOutline,
			"onedrive" => Icons.OneDriveBoxOutline,
			_ => throw new Xunit.Sdk.XunitException($"Unexpected configured cloud provider: {firstProvider}")
		};
		Assert.Contains(providerIcon, cloudLine, StringComparison.Ordinal);
		Assert.EndsWith("  ", cloudLine, StringComparison.Ordinal);
	}

	[Theory]
	[InlineData("googledriverainer", "GoogleDriveRainer")]
	[InlineData("GoogleDriveYebo", "GoogleDriveYebo")]
	[InlineData("onedrivepersonal", "OneDrivePersonal")]
	public void CloudProvider_AcceptsAmafuAccountKeys(string requested, string expected)
	{
		string[] names = ["GoogleDriveRainer", "GoogleDriveYebo", "OneDrivePersonal"];
		Assert.Equal(expected, Program.ResolveConfiguredCloudProvider(requested, names, names));
	}

	[Fact]
	public void CloudProvider_MustBeInDefaultCloudOrder_EvenWhenCloudPathExists()
	{
		var error = Assert.Throws<ArgumentException>(() => Program.ResolveConfiguredCloudProvider(
			"GoogleDrive",
			["OneDrive", "Dropbox"],
			["OneDrive", "Dropbox", "GoogleDrive"]));

		Assert.Contains("not configured as a DefaultDrive on this machine", error.Message);
		Assert.Contains("OneDrive, Dropbox", error.Message);

		var canonical = Program.ResolveConfiguredCloudProvider(
			"onedrive",
			["OneDrive", "Dropbox"],
			["OneDrive", "Dropbox", "GoogleDrive"]);
		Assert.Equal("OneDrive", canonical);
	}

	[Fact]
	public void PitsCommand_LiveSmoke_InvokesRealCliVersion()
	{
		var run = RunPits("--version");
		Assert.Equal(0, run.exitCode);
		Assert.Equal("pits v4.5.6", run.output.Trim());
	}

	[Fact]
	public void MisplacedVerb_FailsFastWithActionableCorrection()
	{
		var run = RunPits("-n", "export", "-c", "OneDrive", "-r", "AIA", "Person", "--json");

		Assert.Equal(2, run.exitCode);
		Assert.Contains("Subcommand 'export' must be the first parameter", run.error);
		Assert.Contains("pits export -n -c OneDrive -r AIA Person --json", run.error);
		Assert.DoesNotContain("export.pit", run.error, StringComparison.Ordinal);
	}

	[Theory]
	[InlineData(true)]
	[InlineData(false)]
	public void VersionFlag_TakesImmediatePrecedence(bool versionFirst)
	{
		var args = versionFirst ? new[] { "-v", "export" } : new[] { "export", "-v" };
		var run = RunPits(args);

		Assert.Equal(0, run.exitCode);
		Assert.Equal("pits v4.5.6", run.output.Trim());
		Assert.Empty(run.error);
	}

	private void CreatePit()
	{
		var source = new TextFile(root, "legacy-person", "json5")
		{
			Lines = ["[{ Id: 'LegacyPerson' }]"],
			Changed = true
		};
		source.Save();
		var seed = RunPits("-n", "-s", source.FullName, "-r", root.FullPath, "Person");
		Assert.Equal(0, seed.exitCode);
	}

	private void CreatePitMarker(string name)
	{
		CreatePit(root / name, name, name);
	}

	private static void CreatePit(RaiPath directory, string itemId, string payload)
	{
		using var pit = new JsonPit.Pit(directory, readOnly: false, autoload: false, unflagged: true);
		var item = new JsonPit.PitItem(itemId);
		item.SetProperty(new { Payload = payload });
		pit.Add(item);
		pit.Save(force: true);
	}

	private static EventFile WriteAuditEvent(
		RaiPath pitPath,
		string pitName,
		DateTimeOffset utc,
		string message)
	{
		var status = new JsonPit.RecoveryStatus(
			JsonPit.RecoveryStatus.CurrentSchemaVersion,
			Guid.NewGuid(),
			utc,
			LogLevel.Information,
			JsonPit.RecoveryStage.Completed,
			pitName,
			"TestMachine",
			"TestMachine-tests-1",
			string.Empty,
			JsonPit.RecoveryRole.Master,
			0,
			0,
			Guid.NewGuid(),
			"Test",
			message,
			string.Empty);
		return new EventFile(
			pitPath,
			$"{utc.UtcTicks}_TestMachine-tests-1_Completed",
			status.ToJObject());
	}

	private void Cleanup()
	{
		try
		{
			if (root.Exists())
				new RaiFile(root.Path).rmdir(depth: 10, deleteFiles: true);
		}
		catch { }
	}

	private static (int exitCode, string output, string error) RunPits(params string[] args)
	{
		var pitsDll = new RaiFile(new RaiPath(AppContext.BaseDirectory), "pits", "dll");
		Assert.True(pitsDll.Exists(), $"Expected pits.dll at {pitsDll.FullName}");

		var result = PitsCommand.ForManagedAssembly(pitsDll).Run(args);
		return (result.ExitCode, result.Output, result.StandardError);
	}
}
