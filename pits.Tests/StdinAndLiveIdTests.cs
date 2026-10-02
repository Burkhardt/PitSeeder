using System.Diagnostics;
using Newtonsoft.Json.Linq;

namespace PitSeeder.Tests;

public sealed class StdinAndLiveIdTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "RAIkeep-cr049-" + Guid.NewGuid().ToString("N"));

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
        Assert.False(Directory.Exists(root));
        Assert.NotEmpty(run.error);
    }

    [Fact]
    public void ReceiptRoundTripsThroughStdin()
    {
        var run = Run("{\"Id\":\"Import001\",\"Class\":\"ImageImport\",\"Note\":\"{value}<tag>\"}", "seed", "Object", "--source", "-", "-r", root, "-n");
        Assert.Equal(0, run.code);
        var export = Run(null, "export", "Object", "--json", "-r", root, "-n");
        Assert.Equal(0, export.code);
        var item = Assert.Single(JArray.Parse(export.output));
        Assert.Equal("ImageImport", item["Class"]!.Value<string>());
        Assert.Equal("{value}<tag>", item["Note"]!.Value<string>());
    }

    [Fact]
    public void MarkerDiagnosticIsExactOnStderr()
    {
        var run = Run("{\"Id\":\"{AdminPersonId}\"}", "seed", "Object", "--source", "-", "-r", root, "-n");
        Assert.Equal("error: Entity Id '{AdminPersonId}' contains a prohibited template marker ('{' or '<'). Resolve template placeholders before writing to a Pit.", run.error.Trim());
        Assert.False(Directory.Exists(root));
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
        var directory = Path.Combine(root, "Object");
        Directory.CreateDirectory(directory);
        File.WriteAllText(Path.Combine(directory, "Object.pit"), new JArray { new JArray { new JObject
        {
            ["Id"] = "{Legacy}", ["Modified"] = DateTimeOffset.UtcNow.AddDays(-1), ["Deleted"] = false
        } } }.ToString());
        var export = Run(null, "export", "Object", "--json", "-r", root, "-n");
        Assert.Equal(0, export.code);
        Assert.Equal("{Legacy}", Assert.Single(JArray.Parse(export.output))["Id"]!.Value<string>());
        var delete = Run(null, "delete-item", "Object", "{Legacy}", "-r", root, "-n");
        Assert.True(delete.code == 0, delete.output + delete.error);
        var empty = Run(null, "export", "Object", "--json", "-r", root, "-n");
        Assert.Equal(0, empty.code);
        Assert.Empty(JArray.Parse(empty.output));
        Assert.Equal(1, Run("{\"Id\":\"{Legacy}\"}", "seed", "Object", "--source", "-", "-r", root, "-n").code);
    }

    [Fact]
    public async Task TypedStdinSeamPreservesPayload()
    {
        var dll = new OsLib.RaiFile(Path.Combine(AppContext.BaseDirectory, "pits.dll"));
        var command = OsLib.PitsCommand.ForManagedAssembly(dll);
        var result = await command.SeedAsync(OsLib.PitsSeedRequest.FromStandardInput("Object", "{\"Id\":\"Typed001\",\"Note\":\"Grüße $HOME `literal`\"}") with
        {
            Options = new OsLib.PitsCommandOptions { PitRoot = new OsLib.RaiPath(root), NoLogo = true }
        }, TestContext.Current.CancellationToken);
        Assert.True(result.Succeeded, result.Output);
        var export = Run(null, "export", "Object", "--json", "-r", root, "-n");
        Assert.Equal("Grüße $HOME `literal`", Assert.Single(JArray.Parse(export.output))["Note"]!.Value<string>());
    }

    private static (int code, string output, string error) Run(string? input, params string[] args)
    {
        var start = new ProcessStartInfo("dotnet") { RedirectStandardInput = true, RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false };
        start.ArgumentList.Add(Path.Combine(AppContext.BaseDirectory, "pits.dll"));
        foreach (var arg in args) start.ArgumentList.Add(arg);
        using var process = Process.Start(start)!;
        var output = process.StandardOutput.ReadToEndAsync();
        var error = process.StandardError.ReadToEndAsync();
        if (input is not null) process.StandardInput.Write(input);
        process.StandardInput.Close();
        Assert.True(process.WaitForExit(30000));
        return (process.ExitCode, output.GetAwaiter().GetResult(), error.GetAwaiter().GetResult());
    }

    public void Dispose() { if (Directory.Exists(root)) Directory.Delete(root, true); }
}
