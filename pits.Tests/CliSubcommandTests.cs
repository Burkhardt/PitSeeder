using Newtonsoft.Json.Linq;
using OsLib;

namespace PitSeeder.Tests;

public sealed class CliSubcommandTests : IDisposable
{
	private readonly RaiPath root = Os.TempDir / "RAIkeep" / "pitseeder-tests" / "cr006-subcommands";

	public CliSubcommandTests()
	{
		Cleanup();
		root.mkdir();
	}

	public void Dispose() => Cleanup();

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
		Assert.Contains("seed, export, audit, delete-property, delete-item", rootHelp.output, StringComparison.OrdinalIgnoreCase);
		Assert.Contains("(default)", rootHelp.output);
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
		Assert.Equal("pits v4.2.5", run.output.Trim());
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

	private void Cleanup()
	{
		try
		{
			if (root.Exists())
				new RaiFile(root.Path).rmdir(depth: 10, deleteFiles: true);
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
