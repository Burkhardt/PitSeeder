using System;
using System.IO;
using System.Reflection;
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
	public static bool Debug { get; set; } = false;
	private static readonly string[] WwwaFiles = { "Person", "Object", "Place", "Activity" };
	public static string? Destination { get; set; }
	public static string? Source { get; set; }
	public static bool Wwwa { get; set; }
	public static bool Banner { get; set; }
	public static string[] Help =>
	[
		$"-h, --help\t\t{Icons.Help}\tprint out all options, replace source and destination with passed-in parameters",
			$"-v, --version\t\t{Icons.Info}\tprint version info",
			$"-n, --nologo\t\t{(Banner ? Icons.Banner : Icons.NoBanner)}\tDo not display the banner",
			$"-s, --source\t\t{Icons.File}|{Icons.Folder}\t{SourceDescription()}",
			$"-d, --destination\t{Icons.File}|{Icons.Folder}\t{DestinationDescription()}",
		$"--wwwa\t\t\t{(Wwwa ? Icons.Success : Icons.NotAvailable)}\tRead 4 JSON/JSON5 files into 4 JsonPits",
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
	/// Example: ./output/Activity.pit
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
				: (EitherFileExists(new RaiFile(new RaiPath(baseLocation), file).FullName, "json5", "json") != null);
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
		return $"{(exists != null ? Icons.Success : Icons.NotAvailable)}\t{baseFile}.{exists}";
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
	/// <returns>The extension of the first existing file, or null if none exist.</returns>
	public static string? EitherFileExists(string? file, string ext1, string? ext2, string? ext3 = null, string? ext4 = null)
	{
		var extensions = new string?[] { ext1, ext2, ext3, ext4 };
		foreach (var extension in extensions)
		{
			if (extension == null)
				continue;
			var f = new RaiFile(file);
			f.Ext = extension;
			if (f.Exists())
				return extension;
		}
		return null;
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
	public static void WriteDebug(string text)
	{
		if (Debug)
			WriteHighlighted(text, ConsoleColor.DarkYellow);
	}
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
		#region Processing_cli_Params
		try
		{
			if (HasOption(args, "-v", "--version", "—v", "—version"))
			{
				Messages.WriteSuccess(GetVersion());
				return 0;
			}
			if (HasOption(args, "-b", "--debug"))
				Messages.Debug = true;
			if (!HasOption(args, "-n", "--nologo", "—n", "—nologo"))
				Messages.WriteBanner($"{Icons.Info} AfricaStage Pit Seeder CLI");
			if (HasOption(args, "-h", "--help", "—h", "—help"))
			{
				Messages.WriteHelp();
				return 0;
			}
			var source = Messages.Source = ParamValue(args, "-s", "--source", "—s", "—source");
			Messages.WriteInfo($"Source: {source}");
			var destination = Messages.Destination = ParamValue(args, "-d", "--destination", "--dest", "—d", "—destination", "—dest") ?? new RaiPath("output").Path;
			Messages.WriteInfo($"Destination: {destination}");
			var wwwa = Messages.Wwwa = HasOption(args, "-wwwa", "--wwwa", "—wwwa");
			Messages.WriteInfo($"WWWA Mode: {wwwa}");
			if (wwwa)
			{
				if (string.IsNullOrWhiteSpace(source) || string.IsNullOrWhiteSpace(destination))
				{
					Messages.WriteError("WWWA mode requires a source directory specified with --source \n\tand a destination directory specified with --destination.");
					return 1;
				}
				var sourceDir = new RaiPath(source.EndsWith(Os.DIR) ? source : source + Os.DIR);
				var destDir = new RaiPath(destination.EndsWith(Os.DIR) ? destination : destination + Os.DIR);
				Messages.WriteInfo($"WWWA RunBulkSeed({sourceDir.Path}, {destDir.Path}) started...");
				var rc = RunBulkSeed(sourceDir, destDir);
				Messages.WriteInfo($"WWWA RunBulkSeed({sourceDir.Path}, {destDir.Path}) completed.");
				Messages.WriteHelp();
				return rc;
			}
			if (source != null)
				return RunSingleSeed(source, destination);
			Messages.WriteHelp();
		}
		catch (Exception ex) { Messages.WriteError($"unknow option; an internal error occurred.\n{ex.Message}"); }
		return 1;
		#endregion Processing_cli_Params
	}
	#region Helpers for argument parsing and WWWA status display
	private static string?ParamValue(string[] options, params string[] aliases)
		=> aliases.Select(a => Array.IndexOf(options, a)).Where(i => i >= 0 && i + 1 < options.Length)
							.Select(i => options[i + 1]).FirstOrDefault();
	private static bool HasOption(string[] options, params string[] aliases)
		=> aliases.Any(options.Contains);
	private static string GetVersion()
	{
		var assembly = Assembly.GetEntryAssembly();
		var name = assembly?.GetName().Name?.ToLowerInvariant() ?? "pits";
		var version = assembly?
			.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
			.InformationalVersion
			.Split('+')[0]
			?? assembly?.GetName().Version?.ToString()
			?? "unknown";
		return $"{name} v{version}";
	}
	#endregion
	#region Seeding Methods
	private static void SeedPit(TextFile source, RaiPath pitDirectory)
	{
		Messages.WriteInfo($"Seeding pit from source file: {source.FullName} to destination directory: {pitDirectory.ToString()}");
		var pitFile = new PitFile(pitDirectory, source.Name);
		Messages.WriteDebug($"Seeding pit from pitFile: {pitFile.FullName}");
		//var pit = new Pit(pitDirectory, readOnly: false);
		var pit = new Pit(pitFile, readOnly: false);
		Messages.WriteDebug($"{Icons.Info} Processing {pit.JsonFile.Name} Pit...");
		pit.AddItems(source.ReadAllText());
		pit.Save();
		Messages.WriteSuccess($"{Icons.Success} Initialized and saved {pit.JsonFile.Name} to {pit.JsonFile.FullName}");
	}
	private static int RunBulkSeed(RaiPath sourceDir, RaiPath destDir)
	{
		Messages.WriteInfo($"{Icons.Info} Initiating WWWA Bulk Seed from: {sourceDir.Path}");
		foreach (var name in new[] { "Person", "Place", "Object", "Activity" })
		{
			var sourceFile = new TextFile(sourceDir, name, ext: "json5");
			Messages.WriteDebug($"SeedPit({sourceFile.FullName}, {(destDir / name).ToString()})...");
			SeedPit(sourceFile, destDir / name);
			Messages.WriteDebug($"SeedPit({sourceFile.FullName}, {(destDir / name).ToString()}) completed.");
		}
		Messages.WriteSuccess($"{Icons.Success} WWWA bulk seeding complete. Data saved to {destDir.ToString()}");
		return 0;
	}
	private static int RunSingleSeed(string sourceFile, string destination)
	{
		var source = new TextFile(sourceFile);
		SeedPit(source, new RaiPath(destination) / source.Name);
		return 0;
	}
	#endregion Seeding Methods
}
