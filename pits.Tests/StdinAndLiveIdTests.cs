using Newtonsoft.Json.Linq;
using OsLib;

namespace PitSeeder.Tests;

public sealed class StdinAndLiveIdTests : IDisposable
{
	private readonly string root = new RaiFile(new RaiPath(Os.TempDir.FullPath), "RAIkeep-cr049-" + Guid.NewGuid().ToString("N")).FullName;

	[Theory]
	[InlineData("[{\"Id\":\"Good\"},{\"Id\":\"{Invalid}\"}]")]
	[InlineData("{\"Id\":\"\\u003cPending>\"}")]
	[InlineData("{\"Id\":\"\\u007bPending}\"}")]
	[InlineData("")]
	[InlineData("not json")]
	public void InvalidStdinRejectsBeforeCreatingTarget(string input)
	{
		var run = Run(input, "seed", "Object", "--source", "-", "-r", root, "-n");
		Assert.Equal(1, run.code);
		Assert.False(new RaiPath(root).Exists());
		Assert.NotEmpty(run.error);
	}

	[Fact]
	public void ReceiptRoundTripsThroughStdin()
	{
		var run = Run("{\"Id\":\"Import001\",\"Class\":\"ImageImport\",\"Note\":\"{value}<tag>\"}", "seed", "Object", "--source", "-", "-r", root, "-n");
		Assert.Equal(0, run.code);
		var export = Run(null, "export", "Object", "--json", "-r", root, "-n");
		Assert.Equal(0, export.code);
		var item = Assert.Single(CliOutputJson.Parse<JArray>(export.output));
		Assert.Equal("ImageImport", item["Class"]!.Value<string>());
		Assert.Equal("{value}<tag>", item["Note"]!.Value<string>());
	}

	[Fact]
	public void MarkerDiagnosticIsExactOnStderr()
	{
		var run = Run("{\"Id\":\"{AdminPersonId}\"}", "seed", "Object", "--source", "-", "-r", root, "-n");
		Assert.Equal("error: Entity Id '{AdminPersonId}' contains a prohibited template marker ('{' or '<'). Resolve template placeholders before writing to a Pit.", run.error.Trim());
		Assert.False(new RaiPath(root).Exists());
	}

	[Fact]
	public void ReceiptPreservesExifOffsetThroughStdinPersistenceAndExport()
	{
		const string original = "2026-09-28T17:40:31+05:30";
		var payload = "{\"Id\":\"ImportExif\",\"Class\":\"ImageImport\",\"Files\":[{\"Exif\":{\"DateTimeOriginal\":\"" + original + "\"}}]}";
		var seed = Run(payload, "seed", "Object", "--source", "-", "-r", root, "-n");
		Assert.True(seed.code == 0, seed.output + seed.error);
		var export = Run(null, "export", "Object", "--json", "-r", root, "-n");
		Assert.Equal(0, export.code);
		using var json = System.Text.Json.JsonDocument.Parse(export.output);
		Assert.Equal(original, json.RootElement[0].GetProperty("Files")[0].GetProperty("Exif").GetProperty("DateTimeOriginal").GetString());
	}

	[Fact]
	public void HistoricalPlaceholderCanBeExportedAndDeletedButNotReinserted()
	{
		var directory = new RaiFile(new RaiPath(root), "Object").FullName;
		new RaiPath(directory).mkdir();
		new TextFile(new RaiPath(directory), "Object.pit").WriteAllText(new JArray { new JArray { new JObject
		{
			["Id"] = "{Legacy}", ["Modified"] = DateTimeOffset.UtcNow.AddDays(-1), ["Deleted"] = false
		} } }.ToString());
		var export = Run(null, "export", "Object", "--json", "-r", root, "-n");
		Assert.Equal(0, export.code);
		Assert.Equal("{Legacy}", Assert.Single(CliOutputJson.Parse<JArray>(export.output))["Id"]!.Value<string>());
		var delete = Run(null, "delete-item", "Object", "{Legacy}", "-r", root, "-n");
		Assert.True(delete.code == 0, delete.output + delete.error);
		var empty = Run(null, "export", "Object", "--json", "-r", root, "-n");
		Assert.Equal(0, empty.code);
		Assert.Empty(CliOutputJson.Parse<JArray>(empty.output));
		Assert.Equal(1, Run("{\"Id\":\"{Legacy}\"}", "seed", "Object", "--source", "-", "-r", root, "-n").code);
	}

	[Fact]
	public async Task TypedStdinSeamPreservesPayload()
	{
		var dll = new OsLib.RaiFile(new RaiFile(new RaiPath(AppContext.BaseDirectory), "pits.dll").FullName);
		var command = OsLib.PitsCommand.ForManagedAssembly(dll);
		var result = await command.SeedAsync(OsLib.PitsSeedRequest.FromStandardInput("Object", "{\"Id\":\"Typed001\",\"Note\":\"Grüße $HOME `literal`\"}") with
		{
			Options = new OsLib.PitsCommandOptions { PitRoot = new OsLib.RaiPath(root), NoLogo = true }
		}, TestContext.Current.CancellationToken);
		Assert.True(result.Succeeded, result.Output);
		var export = Run(null, "export", "Object", "--json", "-r", root, "-n");
		Assert.Equal("Grüße $HOME `literal`", Assert.Single(CliOutputJson.Parse<JArray>(export.output))["Note"]!.Value<string>());
	}

	private static (int code, string output, string error) Run(string? input, params string[] args)
	{
		var assembly = new RaiFile(new RaiPath(AppContext.BaseDirectory), "pits.dll");
		// This fixture sends raw CLI arguments, including rejected inputs, and closes stdin.
		var cli = new RaiSystem("dotnet", new[] { assembly.FullName }.Concat(args)) { StandardInput = input ?? "" };
		var result = cli.ExecResult(30_000);
		Assert.False(result.TimedOut);
		return (result.ExitCode, result.StandardOutput, result.StandardError);
	}

	public void Dispose() { new RaiPath(root).rmdir(deleteFiles: true); }
}
