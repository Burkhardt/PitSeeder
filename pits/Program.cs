using System;
using Newtonsoft.Json.Linq;
using JsonPit;
using OsLib;

public static class Icons
{
	public const string Error = "\uea87";
	public const string Warning = "\uf071";
	public const string Success = "\ueab2";
	public const string Info = "\uea74";
	public const string Help = "\uf059";
}

public static class Messages
{
	public static readonly string[] Help =
	{
		"-h, --help                 \tprint out all options, replace source and destination with passed-in parameters",
		"-n, --no-banner            \tDo not display the banner",
		"-s, --source               \tSingle JSON or JSON5 file or WWWA source directory",
		"-d, --destination          \tJsonPit destination directory",
		"--wwwa                     \tRead 4 JSON/JSON5 files into 4 JsonPits",
		"<source>/Person.json[5]    \t=> <destination>/Person.pit",
		"<source>/Object.json[5]    \t=> <destination>/Object.pit",
		"<source>/Place.json[5]     \t=> <destination>/Place.pit",
		"<source>/Activity.json[5]  \t=> <destination>/Activity.pit",
		$"{Icons.Info} Os.CloudStorageRootDir: {Os.CloudStorageRootDir.Path}",
	};

	public static void WriteHighlighted(string text,
		ConsoleColor foreground = ConsoleColor.Black,
		ConsoleColor? background = null)
	{
		var oldForeground = Console.ForegroundColor;
		var oldBackground = Console.BackgroundColor;

		Console.ForegroundColor = foreground;
		Console.BackgroundColor = background ?? oldBackground;
		Console.WriteLine(text);

		Console.ForegroundColor = oldForeground;
		Console.BackgroundColor = oldBackground;
	}

	public static void WriteError(string text) =>
		WriteHighlighted(text, ConsoleColor.DarkRed, ConsoleColor.White);

	public static void WriteSuccess(string text) =>
		WriteHighlighted(text, ConsoleColor.DarkGreen);

	public static void WriteInfo(string text) =>
		WriteHighlighted(text, ConsoleColor.Blue);

	public static void WriteLine(string text, char underlineChar = '=')
	{
		for (int i = 0; i < text.Length; i++)
			Console.Write(underlineChar);
		Console.WriteLine();
	}

	public static void WriteBanner(string text)
	{
		WriteLine(text);
		Console.WriteLine(text);
		WriteLine(text);
	}

	public static void WriteHelp()
	{
		foreach (var line in Help)
			WriteSuccess(line);
	}
}

internal static class Program
{
	private static int Main(string[] args)
	{
		if (ParamValue(args, "-n", "--no-banner") == null)
			Messages.WriteBanner($"{Icons.Info} AfricaStage Pit Seeder CLI");

		if (ParamValue(args, "-h", "--help") != null)
		{
			Messages.WriteHelp();
			return 0;
		}

		var source = ParamValue(args, "-s", "--source");
		var destination = ParamValue(args, "-d", "--destination") ?? (Os.CloudStorageRootDir / "Nomsa.net" / "AfricaStagePits").Path;
		var wwwa = HasOption(args, "-wwwa", "--wwwa");

		Messages.WriteInfo($"{Icons.Info} Target Storage Root: {destination}");
		if (wwwa)
			return RunBulkSeed(source, destination);

		if (source != null)
			return RunSingleSeed(source, destination);

		Messages.WriteHelp();
		return 1;
	}

	private static int RunBulkSeed(string? source, string destination)
	{
		if (string.IsNullOrWhiteSpace(source))
		{
			Messages.WriteError($"{Icons.Error} Missing source directory for --wwwa.");
			return 1;
		}
		var sourceDir = new RaiPath(source);
		Messages.WriteInfo($"{Icons.Info} Initiating WWWA Bulk Seed from: {sourceDir.Path}");

		SeedPit(new TextFile(new RaiFile(sourceDir, "Person.json5").FullName), destination, "Person");
		SeedPit(new TextFile(new RaiFile(sourceDir, "Place.json5").FullName), destination, "Place");
		SeedPit(new TextFile(new RaiFile(sourceDir, "Object.json5").FullName), destination, "Object");
		SeedPit(new TextFile(new RaiFile(sourceDir, "Activity.json5").FullName), destination, "Activity");
		Messages.WriteSuccess($"{Icons.Success} WWWA bulk seeding complete. Data saved to {destination}");
		return 0;
	}

	private static int RunSingleSeed(string sourceFile, string destination)
	{
		var jsonFile = new TextFile(sourceFile);
		var pitName = new RaiFile(sourceFile).Name;
		SeedPit(jsonFile, destination, pitName);
		return 0;
	}

	private static string? ParamValue(string[] options, string shortOption, string longOption)
	{
		var index = Array.IndexOf(options, shortOption);
		if (index < 0)
			index = Array.IndexOf(options, longOption);

		return index >= 0 && index + 1 < options.Length
			? options[index + 1]
			: null;
	}

	private static bool HasOption(string[] options, string shortOption, string longOption)
	{
		return Array.IndexOf(options, shortOption) >= 0
			|| Array.IndexOf(options, longOption) >= 0;
	}

	private static void SeedPit(TextFile jsonFile, string destination, string pitName)
	{
		Messages.WriteInfo($"{Icons.Info} Processing {pitName} Pit...");

		try
		{
			var jsonContent = string.Join(Environment.NewLine, jsonFile.Lines);
			var jsonArray = JArray.Parse(jsonContent);
			Messages.WriteInfo($"   -> Parsed {jsonArray.Count} items from {jsonFile.NameWithExtension}");

			var targetPitDir = new RaiPath(destination) / pitName;
			var pit = new Pit(values: jsonArray, pitDirectory: targetPitDir.Path, autoload: false, readOnly: false);
			pit.Save(force: true);

			Messages.WriteSuccess($"{Icons.Success} Initialized and saved {pitName} to {targetPitDir.Path}");
		}
		catch (Exception ex)
		{
			Messages.WriteError($"{Icons.Error} Failed to process {pitName}. Details: {ex.Message}");
		}
	}
}