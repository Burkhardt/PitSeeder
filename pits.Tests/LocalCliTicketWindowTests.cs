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
		// v3.13.2 (CR003): master ownership records the exact process identity.
		var apiExactIdentity = ProcessFlagFile.CurrentFlagName("AfricaStage.Api");
		var cliParticipantPrefix = $"{Environment.MachineName}-pits-";

		var apiPit = new Pit(activityPath, readOnly: false, autoload: false, subscriber: "AfricaStage.Api");
		try
		{
			var initialItem = new PitItem("ApiInitialActivity");
			initialItem.SetProperty(new { Source = "AfricaStage.Api", Phase = "Startup" });
			apiPit.Add(initialItem);
			apiPit.Save(force: true);

			Assert.Equal(apiExactIdentity, apiPit.MasterFlag().Originator);

			var expiredTicketTime = DateTimeOffset.UtcNow - MasterFlagFile.TicketDuration - TimeSpan.FromSeconds(10);
			apiPit.MasterFlag().Update(expiredTicketTime, originator: apiExactIdentity);
			Assert.True(apiPit.MasterFlag().IsExpired);

			// A REAL separate CLI process claims the expired lease with its exact PID.
			var firstSeed = WriteSeed("CliSeededActivity", "Seed");
			var firstRun = RunPits("-n", "-s", firstSeed, "-r", root.FullPath, "Activity");
			Assert.Equal(0, firstRun.exitCode);

			var masterAfterCli = new MasterFlagFile(activityPath, "Master");
			Assert.StartsWith(cliParticipantPrefix, masterAfterCli.Originator);
			Assert.False(masterAfterCli.IsExpired);

			var shutdownItem = new PitItem("ApiShutdownFlushActivity");
			shutdownItem.SetProperty(new { Source = "AfricaStage.Api", Phase = "Shutdown" });
			apiPit.Add(shutdownItem);
			apiPit.Save();

			// The API process must not inherit the CLI's valid lease: change files only.
			Assert.StartsWith(cliParticipantPrefix, apiPit.MasterFlag().Originator);
			var changeFiles = apiPit.PitDir.EnumerateFiles("*.json")
				.Where(f => f.Name != apiPit.JsonFile.Name)
				.ToList();
			Assert.Contains(changeFiles, file => ChangeFile.IdentityOf(file.Name) == apiExactIdentity);

			var canonicalBeforeMerge = new TextFile(apiPit.JsonFile.FullName).ReadAllText();
			Assert.Contains("CliSeededActivity", canonicalBeforeMerge);
			Assert.DoesNotContain("ApiShutdownFlushActivity", canonicalBeforeMerge);

			// A second CLI process (new PID, same stable participant) inherits the lease —
			// the previous CLI released its window on disposal — and folds the changes in.
			var secondSeed = WriteSeed("CliMergeTriggerActivity", "MergeTrigger");
			var secondRun = RunPits("-n", "-s", secondSeed, "-r", root.FullPath, "Activity");
			Assert.Equal(0, secondRun.exitCode);

			var canonicalAfterMerge = new TextFile(apiPit.JsonFile.FullName).ReadAllText();
			Assert.Contains("ApiInitialActivity", canonicalAfterMerge);
			Assert.Contains("CliSeededActivity", canonicalAfterMerge);
			Assert.Contains("ApiShutdownFlushActivity", canonicalAfterMerge);
			Assert.Contains("CliMergeTriggerActivity", canonicalAfterMerge);
		}
		finally
		{
			apiPit.Dispose();
		}
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
		// Dispose releases the process-wide canonical-path ownership (CR003 §4).
		using var pit = new Pit(root / name, readOnly: false, autoload: false, unflagged: true);
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

		var result = PitsCommand.ForManagedAssembly(pitsDll).Run(args);
		return (result.ExitCode, result.Output);
	}
}
