using System;
using System.Linq;
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
	public const string NotAvailable = "\ueabd";
	public const string File = "\uea7b";
	public const string Folder = "\uea83";
	public const string Download = "\ueac2";
	public const string Upload = "\ueac3";
	public const string Banner = "\ueb1e";
	public const string NoBanner = "\ueb24";
}

public static class Messages
{
	private static readonly string[] WwwaFiles = { "Person", "Object", "Place", "Activity" };

	public static string? Destination { get; set; }
	public static string? Source { get; set; }
	public static bool Wwwa { get; set; }
	public static bool Banner { get; set; }

	public static string[] Help =>
	[
		$"-h, --help\t\t{Icons.Help}\tprint out all options, replace source and destination with passed-in parameters",
		$"-n, --nologo\t\t{(Banner ? Icons.Banner : Icons.NoBanner)}\tDo not display the banner",
		$"-s, --source\t\t{Icons.File}|{Icons.Folder}\t{SourceDescription()}",
		$"-d, --destination\t{Icons.File}|{Icons.Folder}\t{DestinationDescription()}",
		$"--wwwa\t\t{(Wwwa ? Icons.Success : Icons.NotAvailable)}\tRead 4 JSON/JSON5 files into 4 JsonPits",
		$"\t\tSource: {BuildWwwaStatusIcons(false)}",
		$"\t\tCloud:  {BuildWwwaStatusIcons(true)}",
		$"--source\t\t{Icons.File} {WwwaSourceFileStatus("Person")}",
		$"--destination\t\t{Icons.Upload} {WwwaDestinationFileStatus("Person")}",
		$"--source\t\t{Icons.File} {WwwaSourceFileStatus("Object")}",
		$"--destination\t\t{Icons.Upload} {WwwaDestinationFileStatus("Object")}",
		$"--source\t\t{Icons.File} {WwwaSourceFileStatus("Place")}",
		$"--destination\t\t{Icons.Upload} {WwwaDestinationFileStatus("Place")}",
		$"--source\t\t{Icons.File} {WwwaSourceFileStatus("Activity")}",
		$"--destination\t\t{Icons.Upload} {WwwaDestinationFileStatus("Activity")}",
		$"{Icons.Info} CloudStorageRootDir\t{Icons.Folder} {(Os.CloudStorageRootDir.Exists() ? Icons.Success : Icons.NotAvailable)} {Icons.Upload}\t{Os.CloudStorageRootDir.Path}",
		$"--source:\t\t{(Wwwa ? Icons.Folder : Icons.File)}\t{SourceDisplayPath()}",
		$"--destination:\t\t{(Wwwa ? Icons.Folder : Icons.File)} {Icons.Upload}\t{DestinationDisplayPath()}",
	];

	/// <summary>
	/// /Users/RSB/Library/CloudStorage/OneDrive/OneDriveData/Nomsa.net/Nomsa/Nomsa.Activity.pit
	/// </summary>
	private static string GetCanonicalFullName(string? destination, string nameWithExt)
	{
		var canonicalFile = new CanonicalFile(new RaiPath(destination ?? "."), nameWithExt);
		return canonicalFile.FullName;
	}

	private static string SourceDescription()
	{
		return !Wwwa && !string.IsNullOrWhiteSpace(Source)
			? new RaiFile(Source).FullName
			: "Single JSON or JSON5 file or WWWA source directory";
	}

	private static string DestinationDescription()
	{
		if (!Wwwa && !string.IsNullOrWhiteSpace(Destination))
			return DestinationDisplayPath();

		return "JsonPit destination directory";
	}

	private static string SourceDisplayPath()
	{
		if (string.IsNullOrWhiteSpace(Source))
			return ".";

		return Wwwa ? new RaiPath(Source).Path : new RaiFile(Source).FullName;
	}

	private static string DestinationDisplayPath()
	{
		if (string.IsNullOrWhiteSpace(Destination))
			return ".";

		if (Wwwa)
			return new RaiPath(Destination).Path;

		var pitName = string.IsNullOrWhiteSpace(Source) ? "Default.pit" : new RaiFile(Source).Name + ".pit";
		return GetCanonicalFullName(Destination, pitName);
	}

	private static string WwwaSourceStatus()
	{
		return BuildWwwaStatusIcons(isCloud: false);
	}

	private static string WwwaCloudStatus()
	{
		return BuildWwwaStatusIcons(isCloud: true);
	}

	private static string BuildWwwaStatusIcons(bool isCloud)
	{
		var baseLocation = isCloud ? Destination : Source;
		if (string.IsNullOrWhiteSpace(baseLocation))
			return string.Join(" ", WwwaFiles.Select(_ => Icons.NotAvailable)) + '\t';

		string icons = "";
		foreach (var file in WwwaFiles)
		{
			var exists = isCloud
				? new PitFile(new RaiPath(baseLocation), file).Exists()
				: EitherFileExists(new RaiFile(new RaiPath(baseLocation), file).FullName, "json5", "json");

			if (icons.Length > 0)
				icons += " ";
			icons += exists ? Icons.Success : Icons.NotAvailable;
		}
		icons += '\t';
		return icons;
	}

	private static string WwwaSourceFileStatus(string file)
	{
		if (string.IsNullOrWhiteSpace(Source))
			return $"{Icons.NotAvailable}\t{file}.json5";

		var baseFile = new RaiFile(new RaiPath(Source), file).FullName;
		var exists = EitherFileExists(baseFile, "json5", "json");
		return $"{(exists ? Icons.Success : Icons.NotAvailable)}\t{baseFile}";
	}

	private static string WwwaDestinationFileStatus(string file)
	{
		if (string.IsNullOrWhiteSpace(Destination))
			return $"{Icons.NotAvailable}\t{file}.pit";

		var fullName = GetCanonicalFullName(Destination, file + ".pit");
		var exists = new PitFile(new RaiPath(Destination), file).Exists();
		return $"{(exists ? Icons.Success : Icons.NotAvailable)}\t{fullName}";
	}

	/// <summary>
	/// checks if any of the files exist, trying several extensions
	/// </summary>
	/// <param name="file"></param>
	/// <param name="ext1"></param>
	/// <param name="ext2"></param>
	/// <param name="ext3"></param>
	/// <param name="ext4"></param>
	/// <returns>True if any of the files with the given extensions exist, otherwise false.</returns>
	public static bool EitherFileExists(string? file, string ext1, string? ext2, string? ext3 = null, string? ext4 = null)
	{
		var extensions = new string?[] { ext1, ext2, ext3, ext4 };
		foreach (var extension in extensions)
		{
			if (extension == null)
				continue;
			var f = new RaiFile(file);
			f.Ext = extension;
			if (f.Exists())
				return true;
		}
		return false;
	}

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
		{
			WriteSuccess(line);
		}
	}
}

internal static class Program
{
	private static int Main(string[] args)
	{
		if (ParamValue(args, "-n", "--nologo") == null)
			Messages.WriteBanner($"{Icons.Info} AfricaStage Pit Seeder CLI");

		var source = Messages.Source = ParamValue(args, "-s", "--source");
		var destination = Messages.Destination = ParamValue(args, "-d", "--destination") ?? (Os.CloudStorageRootDir / "Nomsa.net" / "AfricaStagePits").Path;
		var wwwa = Messages.Wwwa = HasOption(args, "-wwwa", "--wwwa");
		var banner = Messages.Banner = !HasOption(args, "-n", "--nologo");

		if (HasOption(args, "-h", "--help"))
		{
			Messages.WriteHelp();
			return 0;
		}

		if (wwwa) {
			var rc = RunBulkSeed(source, destination);
			Messages.WriteHelp();
			return rc;
		}

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

		SeedPit(new RaiFile(sourceDir, "Person", "json5").FullName, destination, "Person");
		SeedPit(new RaiFile(sourceDir, "Place", "json5").FullName, destination, "Place");
		SeedPit(new RaiFile(sourceDir, "Object", "json5").FullName, destination, "Object");
		SeedPit(new RaiFile(sourceDir, "Activity", "json5").FullName, destination, "Activity");
		Messages.WriteSuccess($"{Icons.Success} WWWA bulk seeding complete. Data saved to {destination}");
		return 0;
	}

	private static int RunSingleSeed(string sourceFile, string destination)
	{
		var pitName = new RaiFile(sourceFile).Name;
		SeedPit(sourceFile, destination, pitName);
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

	private static void SeedPit(string sourceFile, string destination, string pitName)
	{
		Messages.WriteInfo($"{Icons.Info} Processing {pitName} Pit...");

		try
		{
			var sourceRaiFile = new RaiFile(sourceFile);
			if (!sourceRaiFile.Exists())
				throw new InvalidOperationException($"Source file not found: {sourceRaiFile.FullName}");

			var jsonFile = new TextFile(sourceFile);
			var jsonContent = jsonFile.ReadAllText();
			if (string.IsNullOrWhiteSpace(jsonContent))
				throw new InvalidOperationException($"Source file is empty or unreadable: {sourceRaiFile.FullName}");

			var jsonArray = JArray.Parse(jsonContent);
			Messages.WriteInfo($"   -> Parsed {jsonArray.Count} items from {jsonFile.NameWithExtension}");

			var pitFile = ResolvePitFile(destination, pitName);
			var pit = new Pit(pitFile);
			pit.AddItems(jsonContent);
			pit.Save();

			Messages.WriteSuccess($"{Icons.Success} Initialized and saved {pitName} to {pitFile.FullName}");
		}
		catch (Exception ex)
		{
			Messages.WriteError($"{Icons.Error} Failed to process {pitName}. Details: {ex.Message}");
		}
	}

	private static PitFile ResolvePitFile(string destination, string pitName)
	{
		var destinationFile = new RaiFile(destination);
		if (string.Equals(destinationFile.Ext, "pit", StringComparison.OrdinalIgnoreCase))
			return new PitFile(new RaiPath(destinationFile.Path), destinationFile.Name);

		return new PitFile(new RaiPath(destination), pitName);
	}
}