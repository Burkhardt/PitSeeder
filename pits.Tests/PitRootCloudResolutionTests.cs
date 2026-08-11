using System.Reflection;
using OsLib;

namespace PitSeeder.Tests;

public class PitRootCloudResolutionTests
{
	[Theory]
	[InlineData("LiveAfricaStage")]
	[InlineData("LiveAfricaStage/")]
	[InlineData("/LiveAfricaStage")]
	public void Help_WithOneDriveCloud_ResolvesPitRootUnderConfiguredCloudRoot(string pitRootArgument)
	{
		var oneDriveRoot = (string?)Os.Config?.Cloud?.OneDrive;
		Assert.False(string.IsNullOrWhiteSpace(oneDriveRoot), "This test requires Os.Config.Cloud.OneDrive to be configured.");

		var expectedPitRoot = new RaiPath(oneDriveRoot).FullPath + "LiveAfricaStage/";

		var (exitCode, output) = InvokePitsMain("-h", "-c", "OneDrive", "-r", pitRootArgument);

		Assert.Equal(0, exitCode);
		Assert.NotNull(Messages.PitRoot);
		Assert.Equal(expectedPitRoot, Messages.PitRoot.FullPath);
		Assert.Contains("-r, --pitroot", output);
		Assert.DoesNotContain($"{Icons.Info} PitRoot\t", output);
		Assert.Contains(expectedPitRoot, output);
	}

	private static (int exitCode, string output) InvokePitsMain(params string[] args)
	{
		ResetMessages();

		var programType = typeof(Messages).Assembly.GetType("Program", throwOnError: true)!;
		var main = programType.GetMethod("Main", BindingFlags.NonPublic | BindingFlags.Static)!;

		var originalOut = Console.Out;
		var originalError = Console.Error;
		using var output = new StringWriter();
		using var error = new StringWriter();

		try
		{
			Console.SetOut(output);
			Console.SetError(error);
			var exitCode = (int)main.Invoke(null, [args])!;
			return (exitCode, output.ToString() + error.ToString());
		}
		finally
		{
			Console.SetOut(originalOut);
			Console.SetError(originalError);
		}
	}

	private static void ResetMessages()
	{
		Messages.Debug = false;
		Messages.CloudProvider = null;
		Messages.PitRoot = null;
		Messages.PitName = null;
		Messages.Source = null;
		Messages.Export = null;
		Messages.Json = false;
		Messages.Wwwa = false;
		Messages.Banner = false;
	}
}
