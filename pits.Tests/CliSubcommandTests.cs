using System.Diagnostics;
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

		var rootHelp = RunPits("--help");
		Assert.Equal(0, rootHelp.exitCode);
		Assert.DoesNotContain("===", rootHelp.output, StringComparison.Ordinal);
		Assert.Contains("seed, export, audit", rootHelp.output, StringComparison.OrdinalIgnoreCase);
		Assert.Contains("(default)", rootHelp.output);
		Assert.DoesNotContain(" PitRoot", rootHelp.output);
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
	public void Version_PrintsPreparedPackageVersion()
	{
		var run = RunPits("--version");
		Assert.Equal(0, run.exitCode);
		Assert.Equal("pits v4.0.1", run.output.Trim());
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
