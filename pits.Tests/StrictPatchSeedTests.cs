using Newtonsoft.Json.Linq;
using OsLib;

namespace PitSeeder.Tests;

/// <summary>
/// CR047 regression coverage derived from Sipho's binding-loss investigation.
/// Every invocation runs the real managed pits CLI through OsLib's typed wrapper.
/// </summary>
public sealed class StrictPatchSeedTests : IDisposable
{
	private readonly RaiPath root = Os.TempDir / "RAIkeep" / "pitseeder-tests" / "cr047-strict-patch";

	public StrictPatchSeedTests()
	{
		Cleanup();
		root.mkdir();
	}

	public void Dispose() => Cleanup();

	[Fact]
	public void TC01_RequireExisting_CommitsSparsePatchForLivingEntity()
	{
		Seed("[{ Id: 'Alice', Kind: 'Person', Name: 'Alice' }]");
		var patch = Source("tc01", "[{ Id: 'Alice', UseCase: 'Live' }]");

		var run = RunPits("seed", "Person", "--source", patch.FullName,
			"--require-existing", "-r", root.FullPath, "-n");

		Assert.Equal(0, run.exitCode);
		Assert.Equal("[pits] Successfully committed 1 entity(ies) to Pit 'Person'.",
			run.output.Split('\n', StringSplitOptions.RemoveEmptyEntries)[^1].TrimEnd('\r'));
		var alice = Assert.Single(Export());
		Assert.Equal("Alice", alice["Name"]?.Value<string>());
		Assert.Equal("Live", alice["UseCase"]?.Value<string>());
	}

	[Fact]
	public void TC02_RequireExisting_RejectsUnknownIdOnStderrWithoutFilesystemMutation()
	{
		Seed("[{ Id: 'Alice', Kind: 'Person', Name: 'Alice' }]");
		var before = SnapshotPitDirectory();
		var patch = Source("tc02", "[{ Id: 'Ghost99', UseCase: 'Live' }]");

		var run = RunPits("seed", "Person", "--source", patch.FullName,
			"--require-existing", "-r", root.FullPath, "-n");

		Assert.Equal(1, run.exitCode);
		Assert.Equal(
			"error: Entity 'Ghost99' does not exist in Pit 'Person'. " +
			"Use without --require-existing / --patch to allow creating new entities.",
			run.error.Trim());
		Assert.Equal(before, SnapshotPitDirectory());
		Assert.DoesNotContain(Export(), item => item["Id"]?.Value<string>() == "Ghost99");
	}

	[Fact]
	public void TC03_PatchAlias_RejectsMixedBatchAtomically()
	{
		Seed("[{ Id: 'Alice', Kind: 'Person', Name: 'Alice' }]");
		var before = SnapshotPitDirectory();
		var patch = Source("tc03",
			"[{ Id: 'Alice', Note: 'must-not-land' }, { Id: 'Ghost99', UseCase: 'Live' }]");

		var run = RunPits("seed", "Person", "--source", patch.FullName,
			"--patch", "-r", root.FullPath, "-n");

		Assert.Equal(1, run.exitCode);
		Assert.Contains("Entity 'Ghost99' does not exist", run.error, StringComparison.Ordinal);
		Assert.Equal(before, SnapshotPitDirectory());
		var alice = Assert.Single(Export());
		Assert.Null(alice["Note"]);
	}

	[Fact]
	public void TC04_RequireExisting_RejectsEmptyBatchBeforeOpeningWritablePit()
	{
		var before = SnapshotPitDirectory();
		var patch = Source("tc04", "[]");

		var run = RunPits("seed", "Person", "--source", patch.FullName,
			"--require-existing", "-r", root.FullPath, "-n");

		Assert.Equal(1, run.exitCode);
		Assert.Equal("error: Patch source contained 0 entities.", run.error.Trim());
		Assert.Equal(before, SnapshotPitDirectory());
		Assert.False((root / "Person").Exists());
	}

	[Fact]
	public void TC05_DefaultSeed_PreservesUnknownIdUpsertAndReportsCount()
	{
		var patch = Source("tc05", "[{ Id: 'Ghost99', UseCase: 'Live' }]");

		var run = RunPits("seed", "Person", "--source", patch.FullName,
			"-r", root.FullPath, "-n");

		Assert.Equal(0, run.exitCode);
		Assert.Contains("[pits] Successfully committed 1 entity(ies) to Pit 'Person'.", run.output);
		var ghost = Assert.Single(Export());
		Assert.Equal("Ghost99", ghost["Id"]?.Value<string>());
		Assert.Equal("Live", ghost["UseCase"]?.Value<string>());
	}

	[Fact]
	public void RequireExisting_TreatsTombstonedEntityAsMissing()
	{
		Seed("[{ Id: 'Alice', Kind: 'Person', Name: 'Alice' }]");
		Assert.Equal(0, RunPits("delete-item", "Person", "Alice", "-r", root.FullPath, "-n").exitCode);
		var before = SnapshotPitDirectory();
		var patch = Source("tombstoned", "[{ Id: 'Alice', Note: 'must-not-revive' }]");

		var run = RunPits("seed", "Person", "--source", patch.FullName,
			"--patch", "-r", root.FullPath, "-n");

		Assert.Equal(1, run.exitCode);
		Assert.Contains("Entity 'Alice' does not exist", run.error, StringComparison.Ordinal);
		Assert.Equal(before, SnapshotPitDirectory());
		Assert.Empty(Export());
	}

	[Fact]
	public void RequireExisting_UnknownIdInAbsentPit_DoesNotCreateTargetDirectory()
	{
		var patch = Source("absent-pit", "[{ Id: 'Ghost99', UseCase: 'Live' }]");

		var run = RunPits("seed", "Person", "--source", patch.FullName,
			"--require-existing", "-r", root.FullPath, "-n");

		Assert.Equal(1, run.exitCode);
		Assert.Contains("Entity 'Ghost99' does not exist", run.error, StringComparison.Ordinal);
		Assert.False((root / "Person").Exists());
	}

	[Fact]
	public void DefaultSeed_AllowsEmptyBatchAndReportsZero()
	{
		var source = Source("empty-default", "[]");

		var run = RunPits("seed", "Person", "--source", source.FullName,
			"-r", root.FullPath, "-n");

		Assert.Equal(0, run.exitCode);
		Assert.Contains("[pits] Successfully committed 0 entity(ies) to Pit 'Person'.", run.output);
	}

	private void Seed(string payload)
	{
		var source = Source("baseline", payload);
		var run = RunPits("seed", "Person", "--source", source.FullName,
			"-r", root.FullPath, "-n");
		Assert.Equal(0, run.exitCode);
	}

	private TextFile Source(string name, string payload)
	{
		var source = new TextFile(root, name, "json5")
		{
			Lines = [payload],
			Changed = true
		};
		source.Save();
		return source;
	}

	private JArray Export()
	{
		var run = RunPits("export", "Person", "--json", "-r", root.FullPath, "-n");
		Assert.Equal(0, run.exitCode);
		return JArray.Parse(run.output[run.output.IndexOf('[')..]);
	}

	private IReadOnlyList<string> SnapshotPitDirectory()
	{
		var pitDirectory = root / "Person";
		if (!pitDirectory.Exists()) return [];
		return Directory.GetFiles(pitDirectory.FullPath, "*", SearchOption.AllDirectories)
			.OrderBy(path => path, StringComparer.Ordinal)
			.Select(path => $"{Path.GetRelativePath(pitDirectory.FullPath, path)}:{Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(path)))}")
			.ToArray();
	}

	private void Cleanup()
	{
		try
		{
			if (root.Exists()) new RaiFile(root.Path).rmdir(depth: 10, deleteFiles: true);
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
