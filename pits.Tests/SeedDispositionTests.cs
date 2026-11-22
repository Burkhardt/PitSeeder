using JsonPit;
using Newtonsoft.Json.Linq;
using OsLib;

namespace PitSeeder.Tests;

public sealed class SeedDispositionTests : IDisposable
{
	private readonly RaiPath cloud = Os.TempDir / ("cr081-" + Guid.NewGuid().ToString("N"));
	private string root => (cloud / "Tenant").FullPath;
	private const string Original = "[{Id:'Alice',Name:'Alice'},{Id:'Bob',Name:'Bob'},{Id:'Carol',Name:'Carol'}]";
	public SeedDispositionTests()
	{
		new RaiPath(root).mkdir();
		// These tests inspect the canonical file immediately after successive saves.
		new TextFile((cloud / "JsonPit").mkdir(), "timing", "json5",
			"{SyncPitDurationSeconds:0,SyncFlagDurationSeconds:0,BufferSeconds:0}");
	}
	public void Dispose() => cloud.rmdir(depth: 8, deleteFiles: true);

	[Theory]
	[InlineData("")]
	[InlineData("-x")]
	[InlineData("--patch")]
	public void InitialRepeatAndMixedBatches_ReportActualHistoryDispositions(string strict)
	{
		var initial = Seed(Original);
		Assert.Equal(0, initial.ExitCode);
		Assert.Contains("3 source items: 3 written, 0 unchanged (Pit 'Person').", initial.Output);
		Assert.Equal(3, FragmentCount());
		var repeat = Seed(Original, [.. strict == "" ? Array.Empty<string>() : new[] { strict }, "--debug"]);
		Assert.Equal(0, repeat.ExitCode);
		Assert.Contains("3 source items: 0 written, 3 unchanged (Pit 'Person').", repeat.Output);
		Assert.Contains("Unchanged IDs: [\"Alice\",\"Bob\",\"Carol\"]", repeat.StandardError);
		Assert.Equal(3, FragmentCount());
		var mixed = Seed(Original.Replace("Name:'Alice'", "Name:'Alice revised'"), strict == "" ? [] : [strict]);
		Assert.Equal(0, mixed.ExitCode);
		Assert.Contains("3 source items: 1 written, 2 unchanged (Pit 'Person').", mixed.Output);
		Assert.Equal(4, FragmentCount());
		Assert.DoesNotContain("Successfully committed", mixed.Output);
	}

	[Fact]
	public void DuplicateIds_AreCountedAsSourceEntries()
	{
		var run = Seed("[{Id:'Alice',Name:'A'},{Id:'Alice',Name:'A'},{Id:'Alice',Name:'B'}]");
		Assert.Equal(0, run.ExitCode);
		Assert.Contains("3 source items: 2 written, 1 unchanged", run.Output);
		Assert.Equal(2, FragmentCount());
	}

	[Fact]
	public void InvalidLaterPayload_IsRejectedBeforeOpeningWritablePit()
	{
		var run = Seed("[{Id:'Alice',Name:'A'},{Id:'Bad',Modified:'2020-01-01T00:00:00Z'}]");
		Assert.NotEqual(0, run.ExitCode);
		Assert.False(new RaiPath(new RaiFile(new RaiPath(root), "Person").FullName).Exists());
		Assert.DoesNotContain("source items:", run.Output);
	}

	[Theory]
	[InlineData("save")]
	[InlineData("dispose")]
	public void PersistenceFailure_UsesCliNonzeroExitBoundaryAndNeverEmitsReceipt(string stage)
	{
		var oldOut = Console.Out;
		var oldError = Console.Error;
		using var output = new StringWriter();
		using var error = new StringWriter();
		var released = false;
		try
		{
			Console.SetOut(output);
			Console.SetError(error);
			var exit = Program.RunCommand("seed", [], () =>
			{
				Program.PersistSeedBatch("Person", [new PitItem("Alice")], _ => true,
					() => { if (stage == "save") throw new IOException("simulated save failure"); },
					() => { released = true; if (stage == "dispose") throw new JsonPitPersistenceException("simulated disposal failure"); });
				return 0;
			});
			Assert.Equal(1, exit);
			Assert.True(released);
			Assert.DoesNotContain("source items:", output.ToString());
			Assert.Contains("simulated", error.ToString());
		}
		finally { Console.SetOut(oldOut); Console.SetError(oldError); }
	}

	[Fact]
	public void Receipt_IsEmittedOnlyAfterSaveAndDisposalFinish()
	{
		var oldOut = Console.Out;
		using var output = new StringWriter();
		var order = new List<string>();
		try
		{
			Console.SetOut(output);
			Program.PersistSeedBatch("Person", [new PitItem("Alice")], _ => { order.Add("add"); return true; },
				() => { order.Add("save"); Assert.DoesNotContain("source items:", output.ToString()); },
				() => { order.Add("dispose"); Assert.DoesNotContain("source items:", output.ToString()); });
			Assert.Equal(new[] { "add", "save", "dispose" }, order);
			Assert.Contains("1 source items: 1 written, 0 unchanged", output.ToString());
		}
		finally { Console.SetOut(oldOut); }
	}

	[Fact]
	public void RetainedWindow_StillReportsOnlyAfterSave_AndPreservesOptOut()
	{
		var run = Seed(Original, ["--window"]);
		Assert.Equal(0, run.ExitCode);
		Assert.Contains("3 source items: 3 written, 0 unchanged", run.Output);
		Assert.Equal(3, FragmentCount());
	}

	private RaiSystemResult Seed(string payload, string[]? options = null)
	{
		var source = new RaiFile(new RaiPath(root), "source.json5").FullName;
		new TextFile(source).WriteAllText(payload);
		return PitsCommand.ForManagedAssembly(new RaiFile(new RaiPath(AppContext.BaseDirectory), "pits", "dll"))
			.Run(["seed", "Person", "--source", source, "-r", new RaiPath(root).FullPath, "-n", .. options ?? []]);
	}
	private int FragmentCount() => CliOutputJson.Parse<JArray>(new TextFile(new RaiFile((new RaiPath(root) / ("Person")), "Person.pit").FullName).ReadAllText())
		.OfType<JArray>().Sum(history => history.Count);
}
