using System.Diagnostics;
using JsonPit;
using OsLib;

namespace PitSeeder.Tests;

public sealed class LocalCliTicketWindowTests : IDisposable
{
	private readonly RaiPath root;
	private readonly TimeSpan originalDuration;

	public LocalCliTicketWindowTests()
	{
		originalDuration = MasterFlagFile.TicketDuration;
		root = Os.TempDir / "RAIkeep" / "pitseeder-tests" / "local-cli-ticket-window";
		Cleanup();
		root.mkdir();
	}

	public void Dispose()
	{
		MasterFlagFile.TicketDuration = originalDuration;
		Cleanup();
	}

	private void Cleanup()
	{
		try
		{
			if (root.Exists())
				new RaiFile(root.Path).rmdir(depth: 10, deleteFiles: true);
		}
		catch
		{
		}
	}

	[Fact]
	public void FiniteReadOnlyCli_ReleasesOwnProcessWindow_AndNextRunSucceeds()
	{
		MasterFlagFile.TicketDuration = TimeSpan.FromMinutes(5);
		CreatePit("Person", "InitialPerson");

		var firstRun = RunPits("-n", "-r", root.FullPath, "Person", "--json");
		var secondRun = RunPits("-n", "-r", root.FullPath, "Person", "--json");

		Assert.Equal(0, firstRun.exitCode);
		Assert.Equal(0, secondRun.exitCode);
		var flags = PitsProcessFlags(root / "Person");
		Assert.NotEmpty(flags);
		Assert.All(flags, flag => Assert.True(flag.IsExpired));
	}

	[Fact]
	public void RetainWindowOption_KeepsFiniteCliWindowActive()
	{
		MasterFlagFile.TicketDuration = TimeSpan.FromMinutes(5);
		CreatePit("Person", "RetainedPerson");

		var run = RunPits("-n", "-r", root.FullPath, "Person", "--json", "--retain-window");

		Assert.Equal(0, run.exitCode);
		Assert.Contains(PitsProcessFlags(root / "Person"), flag => !flag.IsExpired);
	}

	[Fact]
	public void CliException_ReleasesOwnedProcessWindow()
	{
		MasterFlagFile.TicketDuration = TimeSpan.FromMinutes(5);
		CreatePit("Activity", "InitialActivity");
		var invalidSource = new TextFile(root, "invalid-seed", "json5")
		{
			Lines = ["this is not valid json"],
			Changed = true
		};
		invalidSource.Save();

		var run = RunPits("-n", "-s", invalidSource.FullName, "-r", root.FullPath, "Activity");

		Assert.Equal(1, run.exitCode);
		var flags = PitsProcessFlags(root / "Activity");
		Assert.Single(flags);
		Assert.True(flags[0].IsExpired);
	}

	[Fact]
	public void PitsCliAndApiOnSameMachine_WriteCanonicalOrChangeFilesAccordingToTicketOwner()
	{
		MasterFlagFile.TicketDuration = TimeSpan.FromMinutes(5);
		var activityPath = root / "Activity";
		var apiIdentity = ProcessFlagFile.FlagName("AfricaStage.Api");
		var pitsIdentity = ProcessFlagFile.FlagName("pits");

		var apiPit = new Pit(activityPath, readOnly: false, autoload: false, subscriber: "AfricaStage.Api");
		var initialItem = new PitItem("ApiInitialActivity");
		initialItem.SetProperty(new { Source = "AfricaStage.Api", Phase = "Startup" });
		apiPit.Add(initialItem);
		apiPit.Save(force: true);

		Assert.Equal(apiIdentity, apiPit.MasterFlag().Originator);

		var expiredTicketTime = DateTimeOffset.UtcNow - MasterFlagFile.TicketDuration - TimeSpan.FromSeconds(10);
		apiPit.MasterFlag().Update(expiredTicketTime, originator: apiIdentity);
		Assert.True(apiPit.MasterFlag().IsExpired);

		var firstSeed = WriteSeed("CliSeededActivity", "Seed");
		var firstRun = RunPits("-n", "-s", firstSeed, "-r", root.FullPath, "Activity");
		Assert.Equal(0, firstRun.exitCode);

		var masterAfterCli = new MasterFlagFile(activityPath, "Master");
		Assert.Equal(pitsIdentity, masterAfterCli.Originator);
		Assert.False(masterAfterCli.IsExpired);

		var shutdownItem = new PitItem("ApiShutdownFlushActivity");
		shutdownItem.SetProperty(new { Source = "AfricaStage.Api", Phase = "Shutdown" });
		apiPit.Add(shutdownItem);
		apiPit.Save();

		Assert.Equal(pitsIdentity, apiPit.MasterFlag().Originator);
		var changeFiles = apiPit.PitDir.EnumerateFiles("*.json")
			.Where(f => f.Name != apiPit.JsonFile.Name)
			.ToList();
		Assert.Contains(changeFiles, file => file.Name.EndsWith("_" + apiIdentity, StringComparison.OrdinalIgnoreCase));

		var canonicalBeforeMerge = new TextFile(apiPit.JsonFile.FullName).ReadAllText();
		Assert.Contains("CliSeededActivity", canonicalBeforeMerge);
		Assert.DoesNotContain("ApiShutdownFlushActivity", canonicalBeforeMerge);

		var secondSeed = WriteSeed("CliMergeTriggerActivity", "MergeTrigger");
		var secondRun = RunPits("-n", "-s", secondSeed, "-r", root.FullPath, "Activity");
		Assert.Equal(0, secondRun.exitCode);

		var canonicalAfterMerge = new TextFile(apiPit.JsonFile.FullName).ReadAllText();
		Assert.Contains("ApiInitialActivity", canonicalAfterMerge);
		Assert.Contains("CliSeededActivity", canonicalAfterMerge);
		Assert.Contains("ApiShutdownFlushActivity", canonicalAfterMerge);
		Assert.Contains("CliMergeTriggerActivity", canonicalAfterMerge);
	}

	private string WriteSeed(string id, string phase)
	{
		var source = new TextFile(root, id, "json5")
		{
			Lines = [$$"""
		[
		  {
		    "Id": "{{id}}",
		    "Source": "pits",
		    "Phase": "{{phase}}"
		  }
		]
		"""],
			Changed = true
		};
		source.Save();
		return source.FullName;
	}

	private void CreatePit(string name, string itemId)
	{
		var pit = new Pit(root / name, readOnly: false, autoload: false, unflagged: true);
		var item = new PitItem(itemId);
		item.SetProperty(new { Source = "test" });
		pit.Add(item);
		pit.Save(force: true);
	}

	private static List<MasterFlagFile> PitsProcessFlags(RaiPath pitPath) =>
		pitPath.EnumerateFiles($"{Environment.MachineName}-pits-*.flag")
			.Select(file => new MasterFlagFile(file.Path, file.Name))
			.ToList();

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
