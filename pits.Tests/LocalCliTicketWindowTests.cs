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
		root = new RaiPath(Path.GetTempPath()) / "RAIkeep" / "pitseeder-tests" / "local-cli-ticket-window" / Guid.NewGuid().ToString("N");
		root.mkdir();
	}

	public void Dispose()
	{
		MasterFlagFile.TicketDuration = originalDuration;
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

		var canonicalBeforeMerge = File.ReadAllText(apiPit.JsonFile.FullName);
		Assert.Contains("CliSeededActivity", canonicalBeforeMerge);
		Assert.DoesNotContain("ApiShutdownFlushActivity", canonicalBeforeMerge);

		var secondSeed = WriteSeed("CliMergeTriggerActivity", "MergeTrigger");
		var secondRun = RunPits("-n", "-s", secondSeed, "-r", root.FullPath, "Activity");
		Assert.Equal(0, secondRun.exitCode);

		var canonicalAfterMerge = File.ReadAllText(apiPit.JsonFile.FullName);
		Assert.Contains("ApiInitialActivity", canonicalAfterMerge);
		Assert.Contains("CliSeededActivity", canonicalAfterMerge);
		Assert.Contains("ApiShutdownFlushActivity", canonicalAfterMerge);
		Assert.Contains("CliMergeTriggerActivity", canonicalAfterMerge);
	}

	private string WriteSeed(string id, string phase)
	{
		var path = Path.Combine(root.FullPath, id + ".json5");
		File.WriteAllText(path, $$"""
		[
		  {
		    "Id": "{{id}}",
		    "Source": "pits",
		    "Phase": "{{phase}}"
		  }
		]
		""");
		return path;
	}

	private static (int exitCode, string output) RunPits(params string[] args)
	{
		var pitsDll = Path.Combine(AppContext.BaseDirectory, "pits.dll");
		Assert.True(File.Exists(pitsDll), $"Expected pits.dll at {pitsDll}");

		var startInfo = new ProcessStartInfo("dotnet")
		{
			RedirectStandardOutput = true,
			RedirectStandardError = true,
			UseShellExecute = false
		};
		startInfo.ArgumentList.Add(pitsDll);
		foreach (var arg in args)
			startInfo.ArgumentList.Add(arg);

		using var process = Process.Start(startInfo)!;
		var stdout = process.StandardOutput.ReadToEnd();
		var stderr = process.StandardError.ReadToEnd();
		process.WaitForExit();
		return (process.ExitCode, stdout + stderr);
	}
}