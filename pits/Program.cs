using System.Reflection;
using System.Linq;
using JsonPit;
using Microsoft.Extensions.Logging;
using OsLib;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
public static class Icons
{
	public const char Error = '\uea87';
	public const char Warning = '\uf071';
	public const char Success = '\ueab2';
	public const char Info = '\uea74';
	public const char Help = '\uf059';
	public const char NotAvailable = '\ueabd';
	public const char File = '\uea7b';
	public const char Folder = '\uea83';
	public const char Download = '\ueac2';
	public const char Upload = '\ueac3';
	public const char Banner = '\ueb1e';
	public const char NoBanner = '\ueb24';
	public const string DropboxBoxOutline = "\U000F0BF4";
	public const string GoogleDriveBoxOutline = "\U000F0BFD";
	public const string ICloudDriveBoxOutline = "\U000F0C03";
	public const string OneDriveBoxOutline = "\U000F0C15";
	public const string HelpLineWidthCompensation = "  ";
	public static readonly string[] NumberBoxOutlines =
	[
		"\U000F03A6", "\U000F03A9", "\U000F03AC", "\U000F03AE", "\U000F03B0",
		"\U000F03B5", "\U000F03B8", "\U000F03BB", "\U000F03BE"
	];
}
public static class Messages
{
	public static bool Debug { get; set; } = false;
	public static string? CloudProvider { get; set; }
	public static RaiPath? PitRoot { get; set; }
	public static readonly string[] WwwaFiles = { "Person", "Object", "Place", "Activity" };
	public static readonly Dictionary<string, string> WwwaSectionToPit = new()
	{
		{ "Who", "Person" },
		{ "What", "Object" },
		{ "Where", "Place" },
		{ "Activity", "Activity" }
	};
	public static string? PitName { get; set; }
	public static string? Source { get; set; }
	public static string? Export { get; set; }
	public static bool Json { get; set; }
	public static bool Wwwa { get; set; }
	public static bool Banner { get; set; }
	public static bool RetainWindow { get; set; }
	public static bool Events { get; set; }
	public static string[] Help =>
	[
		$"Commands:\t{Icons.Info}\tseed, export, audit, delete-property, delete-item",
		$"  pits seed <PitName> --source <file>",
		$"  pits export (<PitName> | --wwwa) (--out-dir <dir> | --json)",
		$"  pits audit <PitName> [--machine <all|local|name>] [--level <severity>] [--json]",
		$"  pits delete-property <PitName> <ItemId> <PropertyPath>",
		$"  pits delete-item <PitName> <ItemId>",
		$"-h, --help\t{Icons.Help}\tprint out all options",
		$"-v, --version\t{Icons.Info}\tprint version info",
		$"-n, --nologo\t{(Banner ? Icons.Banner : Icons.NoBanner)}\tdo not display the banner",
		$"-b, --debug\t{Icons.Info}\tenable debug output",
		$"-r, --pitroot\t{Icons.Folder}\t{PitRootDescription()}",
		$"-c, --cloud\t{CloudIcon()}\t{CloudDescription()}",
		$"-s, --source\t{Icons.File}\t{SourceDescription()}",
		$"-e, --export\t{Icons.Download}\t{ExportDescription()}",
		$"--json\t\t{(Json ? Icons.Success : Icons.NotAvailable)}\texport to stdout (for piping to jq, grep, etc.)",
		$"--wwwa\t\t{(Wwwa ? Icons.Success : Icons.NotAvailable)}\toperate on all 4 pits (Person, Object, Place, Activity)",
		$"--retain-window\t{Icons.Info}\tkeep this CLI process activity window until its normal timeout",
		$"{Icons.Warning} Legacy\t{Icons.Info}\tflat seed/export flags remain supported in 4.x; use command syntax before 5.x",
		$"{Icons.Info} PitName\t{Icons.File}\t{PitNameDescription()}",
		$"\t\t{Icons.Info}\tpositional arg: pit to operate on, or target pit name when used with -s",
		$"\t\t{Icons.Info}\te.g. 'pits -s patch.json5 -r <root> Activity' seeds Activity.pit from patch.json5",
		$"{Icons.Info} Person\t{WwwaPitStatus("Person")}",
		$"{Icons.Info} Object\t{WwwaPitStatus("Object")}",
		$"{Icons.Info} Place\t\t{WwwaPitStatus("Place")}",
		$"{Icons.Info} Activity\t{WwwaPitStatus("Activity")}",
	];
	private static string PitRootDescription()
	{
		return PitRoot != null
			? PitRoot.FullPath
			: "root directory containing pits";
	}
	private static string CloudDescription()
	{
		var options = CloudProviderOptions();
		return options.Length > 0
			? string.Join(", ", options.Select((name, index) =>
				$"{CloudProviderIcon(name, index + 1)} {name}{(index == 0 ? " (default)" : string.Empty)}"))
			: "no DefaultCloudOrder providers are configured";
	}
	private static string CloudIcon()
	{
		var options = CloudProviderOptions();
		var provider = options.FirstOrDefault(option =>
			string.Equals(option, CloudProvider, StringComparison.OrdinalIgnoreCase));
		return provider != null ? CloudProviderIcon(provider, Array.IndexOf(options, provider) + 1) : Icons.Folder.ToString();
	}
	private static string CloudProviderIcon(string provider, int fallbackNumber)
		=> provider.ToLowerInvariant() switch
		{
			"dropbox" => Icons.DropboxBoxOutline,
			"googledrive" => Icons.GoogleDriveBoxOutline,
			"iclouddrive" => Icons.ICloudDriveBoxOutline,
			"onedrive" => Icons.OneDriveBoxOutline,
			_ => fallbackNumber is > 0 and <= 9
				? Icons.NumberBoxOutlines[fallbackNumber - 1]
				: $"({fallbackNumber})"
		};
	internal static string[] CloudProviderOptions()
	{
		var defaultCloudOrder = new List<string>();
		var configuredCloudProviders = new List<string>();
		try
		{
			dynamic? order = Os.Config?.DefaultCloudOrder;
			if (order != null)
			{
				foreach (var item in order)
				{
					string? name = item?.ToString();
					if (!string.IsNullOrWhiteSpace(name))
						defaultCloudOrder.Add(name);
				}
			}

			dynamic? cloud = Os.Config?.Cloud;
			if (cloud != null)
			{
				IEnumerable<dynamic> properties = cloud.Properties();
				configuredCloudProviders.AddRange(properties
					.Where(property => !string.IsNullOrWhiteSpace(property.Value?.ToString()))
					.Select(property => (string)property.Name));
			}
		}
		catch
		{
			return [];
		}

		return FilterConfiguredDefaultCloudProviders(defaultCloudOrder, configuredCloudProviders);
	}
	internal static string[] FilterConfiguredDefaultCloudProviders(
		IEnumerable<string> defaultCloudOrder,
		IEnumerable<string> configuredCloudProviders)
	{
		var configured = configuredCloudProviders.ToHashSet(StringComparer.OrdinalIgnoreCase);
		return defaultCloudOrder
			.Where(name => !string.IsNullOrWhiteSpace(name) && configured.Contains(name))
			.Distinct(StringComparer.OrdinalIgnoreCase)
			.ToArray();
	}
	private static string SourceDescription()
	{
		if (!string.IsNullOrWhiteSpace(Source))
			return new RaiFile(Source).FullName;
		return "source file for import (JSON or JSON5)";
	}
	private static string ExportDescription()
	{
		if (string.IsNullOrWhiteSpace(Export))
			return "export directory for JSON output";
		if (Wwwa)
			return new RaiFile(new RaiPath(Export), "wwwa", "json").FullName;
		if (!string.IsNullOrWhiteSpace(PitName))
			return new RaiFile(new RaiPath(Export), PitName, "json").FullName;
		return new RaiPath(Export).FullPath;
	}
	private static string PitNameDescription()
	{
		return !string.IsNullOrWhiteSpace(PitName)
			? PitName
			: "pit to operate on or seed target (e.g., Activity)";
	}
	private static string WwwaPitStatus(string name)
	{
		if (PitRoot == null)
			return $"{Icons.NotAvailable}\t{name}.pit";
		var pitFile = new PitFile(PitRoot / name, name);
		return $"{(pitFile.Exists() ? Icons.Success : Icons.NotAvailable)}\t{pitFile.FullName}";
	}
	public static void WriteHighlighted(string text, ConsoleColor foreground = ConsoleColor.Black, ConsoleColor? background = null)
	{
		var oldForeground = Console.ForegroundColor;
		var oldBackground = Console.BackgroundColor;
		Console.ForegroundColor = foreground;
		Console.BackgroundColor = background ?? oldBackground;
		Console.WriteLine(text);
		Console.ForegroundColor = oldForeground;
		Console.BackgroundColor = oldBackground;
	}
	public static void WriteError(string text) => WriteHighlighted(text, ConsoleColor.DarkRed, ConsoleColor.White);
	public static void WriteSuccess(string text) => WriteHighlighted(text, ConsoleColor.DarkGreen);
	public static void WriteInfo(string text) => WriteHighlighted(text, ConsoleColor.Blue);
	public static void WriteDebug(string text) { if (Debug) WriteHighlighted(text, ConsoleColor.DarkYellow); }
	public static void WriteLine(string text, char underlineChar = '─')
	{
		for (int i = 0; i < text.Length; i++) Console.Write(underlineChar);
		Console.WriteLine();
	}
	public static void WriteBanner(string text)
	{
		Console.Write($"{Icons.Banner} ");
		WriteLine(text);
		Console.WriteLine(text);
		Console.Write($"{Icons.Banner} ");
		WriteLine(text);
	}
	public static void WriteHelp()
	{
		foreach (var line in Help) WriteSuccess(line + Icons.HelpLineWidthCompensation);
	}
}
internal static class Program
{
	private const string CliSubscriber = "pits";
	private static readonly object ActivePitsLock = new();
	private static readonly HashSet<Pit> ActivePits = [];
	static Program()
	{
		Console.CancelKeyPress += (_, _) => ReleaseAllProcessWindows();
		AppDomain.CurrentDomain.ProcessExit += (_, _) => ReleaseAllProcessWindows();
	}
	private static int Main(string[] args)
	{
		if (args.Length > 0)
		{
			var command = args[0];
			if (command is "seed" or "export" or "audit" or "delete-property" or "delete-item")
				return RunCommand(command, args[1..]);
		}

		if (HasOption(args, "--events", "--event-machine", "--event-level"))
		{
			Messages.WriteError(
				"The legacy audit flags were replaced in 4.x. Use 'pits audit <PitName> " +
				"[--machine <all|local|name>] [--level <severity>] [--json]'.");
			return 1;
		}

		return RunMappedArguments(args);
	}

	private static int RunMappedArguments(string[] args)
	{
		try
		{
			if (HasOption(args, "-v", "--version"))
			{
				Messages.WriteSuccess(GetVersion());
				return 0;
			}
			#region READ & MAP PARAMETERS
			Messages.Debug = HasOption(args, "-b", "--debug");
			Messages.Banner = !HasOption(args, "-n", "--nologo");
			bool showHelp = HasOption(args, "-h", "--help");
			bool wwwa = Messages.Wwwa = HasOption(args, "-wwwa", "--wwwa");
			bool json = Messages.Json = HasOption(args, "--json");
			Messages.RetainWindow = HasOption(args, "--retain-window");
			bool events = Messages.Events = HasOption(args, "--events");
			string? eventMachine = ParamValue(args, "--event-machine");
			string? eventLevel = ParamValue(args, "--event-level");
			var requestedCloudProvider = ParamValue(args, "-c", "--cloudprovider", "--cloud");
			string? cloudProvider = Messages.CloudProvider = ResolveCloudProvider(requestedCloudProvider);
			var pitRootParam = ParamValue(args, "-r", "--pitroot");
			var sourceParam = Messages.Source = ParamValue(args, "-s", "--source");
			var exportParam = Messages.Export = ParamValue(args, "-e", "--export");
			var pitName = Messages.PitName = PositionalArg(args);
			#endregion
			#region RESOLVE PITROOT
			RaiPath? pitRoot = null;
			if (!string.IsNullOrWhiteSpace(cloudProvider))
			{
				string? cloudDir = Os.Config?.Cloud?[cloudProvider];
				if (string.IsNullOrWhiteSpace(cloudDir))
				{
					Messages.WriteError($"The requested cloud provider '{cloudProvider}' is missing or empty in {Os.DefaultConfigFileLocation}.");
					return 1;
				}
				var cloudRoot = new RaiPath(cloudDir);
				pitRoot = !string.IsNullOrWhiteSpace(pitRootParam)
					? cloudRoot / new RaiRelPath(pitRootParam.TrimStart('/', '\\'))
					: cloudRoot;
			}
			else if (!string.IsNullOrWhiteSpace(pitRootParam))
			{
				pitRoot = new RaiPath(pitRootParam);
			}
			// Infer pitroot from -s if it points to a .pit file and no -r was given.
			if (pitRoot == null && !string.IsNullOrWhiteSpace(sourceParam) && sourceParam.EndsWith(".pit", StringComparison.OrdinalIgnoreCase))
			{
				var sourcePitFile = new PitFile(sourceParam);
				// Canonical structure: pitroot/Name/Name.pit → sourcePitFile.Path = .../Name/
				pitRoot = sourcePitFile.Path.Parent;
			}
			Messages.PitRoot = pitRoot;
			#endregion
			#region VALIDATE & SETUP
			bool hasExecutionIntent = false;
			// Audit mode routes before any Pit construction (CR003): it must not open a Pit,
			// create a process flag, acquire master authority, or write an audit event.
			if (events)
			{
				if (wwwa && !string.IsNullOrWhiteSpace(pitName))
				{
					Messages.WriteError("Audit accepts either one positional pit name or --wwwa, not both.");
					return 1;
				}
				if (!wwwa && string.IsNullOrWhiteSpace(pitName))
				{
					Messages.WriteError("Audit requires one positional pit name or --wwwa.");
					return 1;
				}
				if (pitRoot == null)
				{
					Messages.WriteError("Cannot resolve audit target without -r or --pitroot.");
					return 1;
				}
				var minLevel = LogLevel.Trace;
				if (!string.IsNullOrWhiteSpace(eventLevel) && !PitAudit.TryParseLevel(eventLevel, out minLevel))
				{
					Messages.WriteError($"Invalid --event-level '{eventLevel}'. Valid values: Trace, Debug, Information, Warning, Error, Critical.");
					return 1;
				}
				return wwwa
					? ShowEvents(Messages.WwwaFiles.Select(name => pitRoot / name), eventMachine ?? "all", minLevel, json)
					: ShowEvents([pitRoot / pitName!], eventMachine ?? "all", minLevel, json);
			}
			// WWWA seed mode: -s sourceDir --wwwa -r pitroot
			if (wwwa && !string.IsNullOrWhiteSpace(sourceParam) && !sourceParam.EndsWith(".pit", StringComparison.OrdinalIgnoreCase))
			{
				if (pitRoot == null)
				{
					Messages.WriteError("WWWA seed mode requires a pit root specified with -r or --pitroot.");
					return 1;
				}
				hasExecutionIntent = true;
			}
			// WWWA export mode: --wwwa with -e or --json
			else if (wwwa && (json || !string.IsNullOrWhiteSpace(exportParam)))
			{
				if (pitRoot == null)
				{
					Messages.WriteError("WWWA export requires a pit root specified with -r or --pitroot (or inferred from -s).");
					return 1;
				}
				hasExecutionIntent = true;
			}
			// Single pit export to stdout: Person --json
			else if (!string.IsNullOrWhiteSpace(pitName) && json)
			{
				if (pitRoot == null)
				{
					Messages.WriteError($"Cannot resolve pit '{pitName}' without -r or --pitroot.");
					return 1;
				}
				hasExecutionIntent = true;
			}
			// Single pit export to file: Person -e /tmp/
			else if (!string.IsNullOrWhiteSpace(pitName) && !string.IsNullOrWhiteSpace(exportParam))
			{
				if (pitRoot == null)
				{
					Messages.WriteError($"Cannot resolve pit '{pitName}' without -r or --pitroot.");
					return 1;
				}
				hasExecutionIntent = true;
			}
			// Single seed: -s Person.json5 -r pitroot
			else if (!string.IsNullOrWhiteSpace(sourceParam) && pitRoot != null && !sourceParam.EndsWith(".pit", StringComparison.OrdinalIgnoreCase))
			{
				hasExecutionIntent = true;
			}
			// Single export via -s pointing to a .pit file
			else if (!string.IsNullOrWhiteSpace(sourceParam) && sourceParam.EndsWith(".pit", StringComparison.OrdinalIgnoreCase) && (json || !string.IsNullOrWhiteSpace(exportParam)))
			{
				hasExecutionIntent = true;
			}
			#endregion
			#region LOGGING & HELP
			if (Messages.Banner)
				Messages.WriteBanner($"{Icons.Info} AfricaStage Pit Seeder CLI");
			if (hasExecutionIntent)
			{
				Messages.WriteDebug($"PitRoot: {pitRoot?.FullPath}");
				Messages.WriteDebug($"PitName: {pitName}");
				Messages.WriteDebug($"Source: {sourceParam}");
				Messages.WriteDebug($"Export: {exportParam}");
				Messages.WriteDebug($"Json: {json}");
				Messages.WriteDebug($"WWWA: {wwwa}");
			}
			if (showHelp || !hasExecutionIntent)
			{
				Messages.WriteHelp();
				if (!hasExecutionIntent) return showHelp ? 0 : 1;
			}
			#endregion
			#region REAL WORK EXECUTION
			// WWWA seed
			if (wwwa && !string.IsNullOrWhiteSpace(sourceParam) && !sourceParam.EndsWith(".pit", StringComparison.OrdinalIgnoreCase))
			{
				var sourceDir = new RaiPath(sourceParam.EndsWith(Os.DIR) ? sourceParam : sourceParam + Os.DIR);
				return RunBulkSeed(sourceDir, pitRoot!);
			}
			// WWWA export to file
			if (wwwa && !string.IsNullOrWhiteSpace(exportParam))
			{
				var exportPath = new RaiPath(exportParam);
				return ExportWwwa(pitRoot!, exportPath);
			}
			// WWWA export to stdout
			if (wwwa && json)
			{
				return ExportWwwaToStdout(pitRoot!);
			}
			// Single pit export to stdout via positional name
			if (!string.IsNullOrWhiteSpace(pitName) && json)
			{
				var pitFile = new PitFile(pitRoot! / pitName, pitName);
				return ExportPitToStdout(pitFile);
			}
			// Single pit export to file via positional name
			if (!string.IsNullOrWhiteSpace(pitName) && !string.IsNullOrWhiteSpace(exportParam))
			{
				var pitFile = new PitFile(pitRoot! / pitName, pitName);
				var exportPath = new RaiPath(exportParam);
				return ExportPitToFile(pitFile, exportPath);
			}
			// Single pit export via -s (pointing to .pit file)
			if (!string.IsNullOrWhiteSpace(sourceParam) && sourceParam.EndsWith(".pit", StringComparison.OrdinalIgnoreCase))
			{
				var pitFile = new PitFile(sourceParam);
				if (json)
					return ExportPitToStdout(pitFile);
				if (!string.IsNullOrWhiteSpace(exportParam))
					return ExportPitToFile(pitFile, new RaiPath(exportParam));
			}
			// Single seed: -s source.json5 [PitName] -r pitroot
			// Target pit name is the trailing positional arg if provided, else the source file name.
			if (!string.IsNullOrWhiteSpace(sourceParam) && pitRoot != null)
			{
				var sourceFile = new TextFile(sourceParam);
				if (!sourceFile.Exists())
				{
					Messages.WriteError($"Source file '{sourceFile.FullName}' does not exist.");
					return 1;
				}
				var name = !string.IsNullOrWhiteSpace(pitName) ? pitName : sourceFile.Name;
				var pitFile = new PitFile(pitRoot / name, name);
				SeedPit(sourceFile, pitFile);
				return 0;
			}
			#endregion
		}
		catch (ArgumentException ex)
		{
			Messages.WriteError($"CLI Error: {ex.Message}");
		}
		catch (Exception ex)
		{
			Messages.WriteError($"An internal error occurred.\n{ex.Message}");
		}
		return 1;
	}

	private static int RunCommand(string command, string[] args)
	{
		try
		{
			if (HasOption(args, "-h", "--help"))
			{
				WriteCommandHelp(command);
				return 0;
			}

			return command switch
			{
				"seed" => RunSeedCommand(args),
				"export" => RunExportCommand(args),
				"audit" => RunAuditCommand(args),
				"delete-property" => RunDeletePropertyCommand(args),
				"delete-item" => RunDeleteItemCommand(args),
				_ => throw new ArgumentException($"Unknown command '{command}'.")
			};
		}
		catch (ArgumentException ex)
		{
			Messages.WriteError($"CLI Error: {ex.Message}");
			Messages.WriteInfo($"Run 'pits {command} --help' for command usage.");
			return 1;
		}
		catch (Exception ex)
		{
			Messages.WriteError($"An internal error occurred.\n{ex.Message}");
			return 1;
		}
	}

	private static int RunSeedCommand(string[] args)
	{
		var valueOptions = GlobalValueOptions.Concat(["--source"]).ToHashSet(StringComparer.Ordinal);
		var allowed = GlobalSwitchOptions.Concat(valueOptions).Concat(["--wwwa"]).ToHashSet(StringComparer.Ordinal);
		var positionals = ValidateCommandTokens(args, allowed, valueOptions);
		var wwwa = HasOption(args, "--wwwa");
		var source = ParamValue(args, "--source");

		if (string.IsNullOrWhiteSpace(source))
			throw new ArgumentException("seed requires --source <file-or-directory>.");
		if (wwwa && positionals.Count > 0)
			throw new ArgumentException("seed accepts either <PitName> or --wwwa, not both.");
		if (!wwwa && positionals.Count != 1)
			throw new ArgumentException("seed requires exactly one <PitName>, or --wwwa for the four-pit source directory.");

		return RunMappedArguments(args);
	}

	private static int RunExportCommand(string[] args)
	{
		var valueOptions = GlobalValueOptions.Concat(["--out-dir"]).ToHashSet(StringComparer.Ordinal);
		var allowed = GlobalSwitchOptions.Concat(valueOptions).Concat(["--json", "--wwwa"]).ToHashSet(StringComparer.Ordinal);
		var positionals = ValidateCommandTokens(args, allowed, valueOptions);
		var wwwa = HasOption(args, "--wwwa");
		var json = HasOption(args, "--json");
		var outDir = ParamValue(args, "--out-dir");

		if (wwwa && positionals.Count > 0)
			throw new ArgumentException("export accepts either <PitName> or --wwwa, not both.");
		if (!wwwa && positionals.Count != 1)
			throw new ArgumentException("export requires exactly one <PitName>, or --wwwa.");
		if (json == !string.IsNullOrWhiteSpace(outDir))
			throw new ArgumentException("export requires exactly one output mode: --json or --out-dir <dir>.");

		return RunMappedArguments(ReplaceOption(args, "--out-dir", "--export"));
	}

	private static int RunAuditCommand(string[] args)
	{
		var valueOptions = GlobalValueOptions.Concat(["--machine", "--level"]).ToHashSet(StringComparer.Ordinal);
		var allowed = GlobalSwitchOptions.Concat(valueOptions).Concat(["--json", "--wwwa"]).ToHashSet(StringComparer.Ordinal);
		var positionals = ValidateCommandTokens(args, allowed, valueOptions);
		var wwwa = HasOption(args, "--wwwa");

		if (wwwa && positionals.Count > 0)
			throw new ArgumentException("audit accepts either <PitName> or --wwwa, not both.");
		if (!wwwa && positionals.Count != 1)
			throw new ArgumentException("audit requires exactly one <PitName>, or --wwwa.");

		var mapped = ReplaceOption(args, "--machine", "--event-machine");
		mapped = ReplaceOption(mapped, "--level", "--event-level");
		return RunMappedArguments([.. mapped, "--events"]);
	}

	private static int RunDeletePropertyCommand(string[] args)
	{
		var valueOptions = GlobalValueOptions.ToHashSet(StringComparer.Ordinal);
		var allowed = GlobalSwitchOptions.Concat(valueOptions).ToHashSet(StringComparer.Ordinal);
		var positionals = ValidateCommandTokens(args, allowed, valueOptions);
		if (positionals.Count != 3)
			throw new ArgumentException(
				"delete-property requires exactly <PitName> <ItemId> <PropertyPath>.");
		ValidatePropertyPath(positionals[2]);
		return RunDeleteMutation(args, positionals[0], positionals[1], positionals[2]);
	}

	private static int RunDeleteItemCommand(string[] args)
	{
		var valueOptions = GlobalValueOptions.ToHashSet(StringComparer.Ordinal);
		var allowed = GlobalSwitchOptions.Concat(valueOptions).ToHashSet(StringComparer.Ordinal);
		var positionals = ValidateCommandTokens(args, allowed, valueOptions);
		if (positionals.Count != 2)
			throw new ArgumentException("delete-item requires exactly <PitName> <ItemId>.");
		return RunDeleteMutation(args, positionals[0], positionals[1], propertyPath: null);
	}

	private static int RunDeleteMutation(
		string[] args,
		string pitName,
		string itemId,
		string? propertyPath)
	{
		if (string.IsNullOrWhiteSpace(pitName) || string.IsNullOrWhiteSpace(itemId))
			throw new ArgumentException("PitName and ItemId must be non-empty values.");

		Messages.Debug = HasOption(args, "-b", "--debug");
		Messages.Banner = !HasOption(args, "-n", "--nologo");
		Messages.RetainWindow = HasOption(args, "--retain-window");
		var requestedCloudProvider = ParamValue(args, "-c", "--cloudprovider", "--cloud");
		var cloudProvider = Messages.CloudProvider = ResolveCloudProvider(requestedCloudProvider);
		var pitRootParam = ParamValue(args, "-r", "--pitroot");
		RaiPath? pitRoot = null;
		if (!string.IsNullOrWhiteSpace(cloudProvider))
		{
			string? cloudDirectory = Os.Config?.Cloud?[cloudProvider];
			if (string.IsNullOrWhiteSpace(cloudDirectory))
				throw new ArgumentException(
					$"The requested cloud provider '{cloudProvider}' is missing or empty in {Os.DefaultConfigFileLocation}.");
			var cloudRoot = new RaiPath(cloudDirectory);
			pitRoot = !string.IsNullOrWhiteSpace(pitRootParam)
				? cloudRoot / new RaiRelPath(pitRootParam.TrimStart('/', '\\'))
				: cloudRoot;
		}
		else if (!string.IsNullOrWhiteSpace(pitRootParam))
		{
			pitRoot = new RaiPath(pitRootParam);
		}

		if (pitRoot is null)
			throw new ArgumentException(
				$"Cannot resolve pit '{pitName}' without -r or --pitroot, or a configured -c or --cloud provider.");

		Messages.PitRoot = pitRoot;
		Messages.PitName = pitName;
		if (Messages.Banner)
			Messages.WriteBanner($"{Icons.Info} AfricaStage Pit Seeder CLI");

		var pitFile = new PitFile(pitRoot / pitName, pitName);
		if (!pitFile.Exists())
		{
			Messages.WriteError($"Pit file '{pitFile.FullName}' does not exist.");
			return 1;
		}

		var pit = TrackPit(new Pit(pitFile, subscriber: CliSubscriber, readOnly: false));
		try
		{
			var item = pit[itemId];
			if (item is null)
			{
				Messages.WriteError($"Item '{itemId}' does not exist in pit '{pitName}'.");
				return 1;
			}

			if (propertyPath is null)
			{
				if (!pit.Delete(itemId))
					return 1;
			}
			else
			{
				item.DeletePropertyPath(propertyPath);
				pit.Add(item);
			}

			pit.Save();
			var operation = propertyPath is null
				? $"Deleted item '{itemId}'"
				: $"Deleted property '{propertyPath}' from item '{itemId}'";
			Messages.WriteSuccess($"{Icons.Success} {operation} in {pitFile.FullName}");
			return 0;
		}
		finally
		{
			ReleaseProcessWindow(pit);
		}
	}

	private static void ValidatePropertyPath(string propertyPath)
	{
		if (string.IsNullOrWhiteSpace(propertyPath) ||
			propertyPath.Split('.', StringSplitOptions.None).Any(string.IsNullOrWhiteSpace))
			throw new ArgumentException(
				"PropertyPath must contain non-empty dot-delimited property names.");
	}

	private static readonly string[] GlobalValueOptions =
	[
		"-r", "--pitroot", "-c", "--cloudprovider", "--cloud"
	];

	private static readonly string[] GlobalSwitchOptions =
	[
		"-h", "--help", "-v", "--version", "-b", "--debug", "-n", "--nologo", "--retain-window"
	];

	private static List<string> ValidateCommandTokens(
		string[] args,
		IReadOnlySet<string> allowedOptions,
		IReadOnlySet<string> valueOptions)
	{
		var positionals = new List<string>();
		for (var i = 0; i < args.Length; i++)
		{
			var token = args[i];
			if (!token.StartsWith("-", StringComparison.Ordinal))
			{
				positionals.Add(token);
				continue;
			}
			if (!allowedOptions.Contains(token))
				throw new ArgumentException($"Unknown option '{token}'.");
			if (!valueOptions.Contains(token))
				continue;
			if (i + 1 >= args.Length || args[i + 1].StartsWith("-", StringComparison.Ordinal))
				throw new ArgumentException($"The option '{token}' requires a value.");
			i++;
		}
		return positionals;
	}

	private static string[] ReplaceOption(string[] args, string source, string target)
		=> args.Select(token => token == source ? target : token).ToArray();

	private static string? ResolveCloudProvider(string? requestedCloudProvider)
	{
		if (string.IsNullOrWhiteSpace(requestedCloudProvider))
			return null;

		return ResolveAllowedCloudProvider(requestedCloudProvider, Messages.CloudProviderOptions());
	}

	internal static string? ResolveConfiguredCloudProvider(
		string? requestedCloudProvider,
		IEnumerable<string> defaultCloudOrder,
		IEnumerable<string> configuredCloudProviders)
	{
		if (string.IsNullOrWhiteSpace(requestedCloudProvider))
			return null;

		var allowed = Messages.FilterConfiguredDefaultCloudProviders(
			defaultCloudOrder,
			configuredCloudProviders);
		return ResolveAllowedCloudProvider(requestedCloudProvider, allowed);
	}

	private static string ResolveAllowedCloudProvider(
		string requestedCloudProvider,
		IReadOnlyList<string> allowed)
	{
		var resolved = allowed.FirstOrDefault(provider =>
			string.Equals(provider, requestedCloudProvider, StringComparison.OrdinalIgnoreCase));
		if (resolved != null)
			return resolved;

		var available = allowed.Count > 0 ? string.Join(", ", allowed) : "none";
		throw new ArgumentException(
			$"The cloud provider '{requestedCloudProvider}' is not configured as a DefaultDrive on this machine. " +
			$"Configured DefaultCloudOrder options: {available}.");
	}

	private static void WriteCommandHelp(string command)
	{
		var lines = command switch
		{
			"seed" => new[]
			{
				"Usage: pits seed <PitName> --source <file> [global options]",
				"       pits seed --wwwa --source <directory> [global options]",
				"Imports JSON/JSON5 into one pit or the four WWWA pits."
			},
			"export" => new[]
			{
				"Usage: pits export (<PitName> | --wwwa) (--out-dir <dir> | --json) [global options]",
				"Exports one pit or a resolved WWWA projection to files or standard output."
			},
			"audit" => new[]
			{
				"Usage: pits audit (<PitName> | --wwwa) [--machine <all|local|name>] [--level <severity>] [--json] [global options]",
				"Reads durable events without opening a Pit or creating coordination artifacts."
			},
			"delete-property" => new[]
			{
				"Usage: pits delete-property <PitName> <ItemId> <PropertyPath> [global options]",
				"Appends a property tombstone; PropertyPath accepts dot notation such as What.Chat."
			},
			"delete-item" => new[]
			{
				"Usage: pits delete-item <PitName> <ItemId> [global options]",
				"Appends an item tombstone so projected reads and exports omit the item."
			},
			_ => Array.Empty<string>()
		};
		foreach (var line in lines)
			Messages.WriteSuccess(line);
		Messages.WriteInfo("Global options: -r|--pitroot, -c|--cloud, -b|--debug, -n|--nologo, --retain-window");
	}
	#region Helpers for argument parsing
	private static readonly string[] SwitchesWithValues = { "-s", "--source", "-r", "--pitroot", "-e", "--export", "-c", "--cloudprovider", "--cloud", "--event-machine", "--event-level" };
	private static string? ParamValue(string[] options, params string[] aliases)
		=> aliases.Select(a => Array.IndexOf(options, a)).Where(i => i >= 0)
			.Select(i => i + 1 < options.Length && !options[i + 1].StartsWith("-")
				? options[i + 1]
				: throw new ArgumentException($"The option '{options[i]}' requires a value."))
			.FirstOrDefault();
	private static bool HasOption(string[] options, params string[] aliases)
		=> aliases.Any(options.Contains);
	/// <summary>
	/// Finds the first positional argument (not a switch, not a value of a switch).
	/// </summary>
	private static string? PositionalArg(string[] args)
	{
		for (int i = 0; i < args.Length; i++)
		{
			if (SwitchesWithValues.Contains(args[i]))
			{
				i++; // skip the value that follows
				continue;
			}
			if (args[i].StartsWith("-")) continue; // boolean switch
			return args[i]; // positional arg
		}
		return null;
	}
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
	#region Events audit mode
	/// <summary>
	/// Read-only audit of a pit's durable events (CR003): reads EventDirectory content via
	/// JsonPit's PitAudit without opening a Pit, creating a process flag, acquiring master
	/// authority, or writing an audit event. Output is deterministic: ordered by machine,
	/// UTC time, and event identity.
	/// </summary>
	private static int ShowEvents(IEnumerable<RaiPath> pitDirectories, string machineFilter, LogLevel minLevel, bool json)
	{
		var directories = pitDirectories.ToList();
		var events = directories
			.SelectMany(directory => PitAudit.Read(directory, machineFilter, minLevel))
			.OrderBy(e => e.Machine, StringComparer.Ordinal)
			.ThenBy(e => e.UtcTime)
			.ThenBy(e => e.EventId, StringComparer.Ordinal)
			.ToList();
		if (json)
		{
			var array = new JArray(events.Select(e => e.Content));
			Console.WriteLine(array.ToString(Formatting.Indented));
			return 0;
		}
		if (events.Count == 0)
		{
			Messages.WriteInfo($"No matching events under {string.Join(", ", directories.Select(directory => directory.FullPath + OsLib.EventDirectory.Name))}.");
			return 0;
		}
		foreach (var e in events)
			Console.WriteLine($"{e.Machine}\t{e.UtcTime:o}\t{e.Level}\t{e.Stage}\t{e.Message}\t({e.FileName})");
		return 0;
	}
	#endregion
	#region Seeding Methods
	private static void SeedPit(TextFile source, PitFile pitFile)
	{
		Messages.WriteInfo($"Seeding pit from source file: {source.FullName} \n\tto destination: {pitFile.FullName}");
		var pit = TrackPit(new Pit(pitFile, subscriber: CliSubscriber, readOnly: false));
		try
		{
			Messages.WriteDebug($"{Icons.Info} Processing {pit.JsonFile.Name} Pit...");
			var payload = source.ReadAllText();
			var root = JToken.Parse(payload);
			JArray itemsArray = root switch
			{
				JArray arr => arr,
				// Keyed object map: { "Id1": { ... }, "Id2": { ... } } → take the values
				JObject obj => new JArray(obj.Properties().Select(p => p.Value)),
				_ => throw new ArgumentException(
					$"Source '{source.FullName}' must be a JSON array or keyed object map; got {root.Type}.")
			};
			pit.AddItems(itemsArray.ToString());
			pit.Save();
			Messages.WriteSuccess($"{Icons.Success} Initialized and saved {pit.JsonFile.Name} to {pit.JsonFile.FullName}");
		}
		finally
		{
			ReleaseProcessWindow(pit);
		}
	}
	private static int RunBulkSeed(RaiPath sourceDir, RaiPath pitRoot)
	{
		Messages.WriteInfo($"{Icons.Info} Initiating WWWA Bulk Seed from: {sourceDir.Path}");
		foreach (var name in Messages.WwwaFiles)
		{
			var sourceFile = new TextFile(sourceDir, name, ext: "json5");
			var targetPitFile = new PitFile(pitRoot / name, name);
			Messages.WriteDebug($"SeedPit({sourceFile.FullName}, {targetPitFile.FullName})...");
			SeedPit(sourceFile, targetPitFile);
			Messages.WriteDebug($"SeedPit({sourceFile.FullName}, {targetPitFile.FullName}) completed.");
		}
		Messages.WriteSuccess($"{Icons.Success} WWWA bulk seeding complete. Data saved to {pitRoot.Path}");
		return 0;
	}
	#endregion
	#region Export Methods
	private static int ExportPitToFile(PitFile pitFile, RaiPath exportPath)
	{
		if (!pitFile.Exists())
		{
			Messages.WriteError($"Pit file '{pitFile.FullName}' does not exist.");
			return 1;
		}
		Messages.WriteInfo($"Exporting pit: {pitFile.FullName}");
		var pit = TrackPit(new Pit(pitFile, subscriber: CliSubscriber, readOnly: true));
		try
		{
			exportPath.mkdir();
			pit.ExportJson(exportPath);
			var exportFile = new RaiFile(exportPath, pit.JsonFile.Name, "json");
			Messages.WriteSuccess($"{Icons.Success} Exported {pit.JsonFile.Name} to {exportFile.FullName}");
			return 0;
		}
		finally
		{
			ReleaseProcessWindow(pit);
		}
	}
	private static int ExportPitToStdout(PitFile pitFile)
	{
		if (!pitFile.Exists())
		{
			Messages.WriteError($"Pit file '{pitFile.FullName}' does not exist.");
			return 1;
		}
		var pit = TrackPit(new Pit(pitFile, subscriber: CliSubscriber, readOnly: true));
		try
		{
			var items = new JArray();
			foreach (var key in pit.Keys)
			{
				var item = pit[key];
				if (item is not null) items.Add(item);
			}
			Console.WriteLine(items.ToString(Formatting.Indented));
			return 0;
		}
		finally
		{
			ReleaseProcessWindow(pit);
		}
	}
	private static int ExportWwwa(RaiPath pitRoot, RaiPath exportPath)
	{
		var resolved = BuildResolvedWwwa(pitRoot);
		if (resolved == null) return 1;
		exportPath.mkdir();
		var exportFile = new RaiFile(exportPath, "wwwa", "json");
		var textFile = new TextFile(exportFile.FullName)
		{
			Lines = [resolved.ToString(Formatting.Indented)],
			Changed = true
		};
		textFile.Save();
		Messages.WriteSuccess($"{Icons.Success} Exported resolved WWWA to {exportFile.FullName}");
		return 0;
	}
	private static int ExportWwwaToStdout(RaiPath pitRoot)
	{
		var resolved = BuildResolvedWwwa(pitRoot);
		if (resolved == null) return 1;
		Console.WriteLine(resolved.ToString(Formatting.Indented));
		return 0;
	}
	/// <summary>
	/// Builds the resolved WWWA export: loads all 4 pits, exports their items,
	/// and resolves one level of foreign key references (Who/What/Where/Activity).
	/// Resolved wrappers dissolve; unresolved wrappers remain.
	/// </summary>
	private static JObject? BuildResolvedWwwa(RaiPath pitRoot)
	{
		var pits = new Dictionary<string, Pit>();
		try
		{
			// Load all 4 pits and build lookup dictionaries
			var lookups = new Dictionary<string, Dictionary<string, JObject>>();
			foreach (var name in Messages.WwwaFiles)
			{
				var pitFile = new PitFile(pitRoot / name, name);
				if (!pitFile.Exists())
				{
					Messages.WriteError($"Pit file '{pitFile.FullName}' does not exist.");
					return null;
				}
				var pit = TrackPit(new Pit(pitFile, subscriber: CliSubscriber, readOnly: true));
				pits[name] = pit;
				var lookup = new Dictionary<string, JObject>(StringComparer.Ordinal);
				foreach (var key in pit.Keys)
				{
					var item = pit[key];
					if (item is JObject obj)
						lookup[key] = obj;
				}
				lookups[name] = lookup;
			}
			// Export each pit with resolved references
			var result = new JObject();
			foreach (var name in Messages.WwwaFiles)
			{
				var items = new JArray();
				foreach (var key in pits[name].Keys)
				{
					var item = pits[name][key];
					if (item is JObject obj)
						items.Add(ResolveWwwaReferences(obj, lookups));
					else if (item is not null)
						items.Add(item);
				}
				result[name] = items;
			}
			return result;
		}
		finally
		{
			foreach (var pit in pits.Values)
				ReleaseProcessWindow(pit);
		}
	}
	private static void ReleaseProcessWindow(Pit pit)
	{
		if (Messages.RetainWindow)
		{
			// Opt-out: keep the activity window until its normal timeout. Accepted data is
			// already durable through Save(); the full disposal sequence would release it.
			lock (ActivePitsLock)
				ActivePits.Remove(pit);
			return;
		}
		try
		{
			// CR003 durability boundary: Dispose publishes the tenure write set plus dirty
			// fragments as ordinary change files, optionally completes a canonical save,
			// then releases the process window, watcher, and path registration.
			pit.Dispose();
			Messages.WriteDebug($"Disposed pit {pit.JsonFile.Name} and released its process activity window.");
			lock (ActivePitsLock)
				ActivePits.Remove(pit);
		}
		catch (Exception ex)
		{
			Messages.WriteDebug($"Could not release the process activity window for {pit.JsonFile.Name}; process-exit cleanup will retry: {ex.Message}");
		}
	}
	private static Pit TrackPit(Pit pit)
	{
		lock (ActivePitsLock)
			ActivePits.Add(pit);
		return pit;
	}
	private static void ReleaseAllProcessWindows()
	{
		Pit[] active;
		lock (ActivePitsLock)
			active = ActivePits.ToArray();
		foreach (var pit in active)
		{
			try { ReleaseProcessWindow(pit); }
			catch { }
		}
	}
	/// <summary>
	/// Resolves one level of WWWA foreign key references in a pit item.
	/// For each Who/What/Where/Activity section, tries to resolve every value
	/// against the corresponding pit. Resolved keys are promoted to the item level.
	/// If all keys in a section resolve, the wrapper is removed entirely.
	/// Unresolved keys remain inside the wrapper.
	/// </summary>
	private static JObject ResolveWwwaReferences(JObject item, Dictionary<string, Dictionary<string, JObject>> lookups)
	{
		var resolved = new JObject(item);
		foreach (var (section, pitName) in Messages.WwwaSectionToPit)
		{
			if (resolved[section] is not JObject sectionObj) continue;
			if (!lookups.TryGetValue(pitName, out var lookup)) continue;
			var promoted = new List<(string key, JToken value)>();
			var unresolved = new JObject();
			foreach (var prop in sectionObj.Properties())
			{
				if (prop.Value.Type == JTokenType.String)
				{
					// Single foreign key: "Performer": "Nomsa"
					var id = prop.Value.ToString();
					if (lookup.TryGetValue(id, out var found))
						promoted.Add((prop.Name, found.DeepClone()));
					else
						unresolved[prop.Name] = prop.Value;
				}
				else if (prop.Value is JArray arr && arr.All(t => t.Type == JTokenType.String))
				{
					// Array of foreign keys: "ShowImages": ["SDZSP26Img"]
					var resolvedArray = new JArray();
					bool allResolved = true;
					foreach (var element in arr)
					{
						var id = element.ToString();
						if (lookup.TryGetValue(id, out var found))
							resolvedArray.Add(found.DeepClone());
						else
						{
							resolvedArray.Add(element);
							allResolved = false;
						}
					}
					if (allResolved)
						promoted.Add((prop.Name, resolvedArray));
					else
						unresolved[prop.Name] = prop.Value;
				}
				else
				{
					// Not a foreign key reference, keep as-is
					unresolved[prop.Name] = prop.Value;
				}
			}
			// Remove the wrapper
			resolved.Remove(section);
			// Add promoted (resolved) properties to item level
			foreach (var (key, value) in promoted)
				resolved[key] = value;
			// If any unresolved keys remain, keep the wrapper with just those
			if (unresolved.HasValues)
				resolved[section] = unresolved;
		}
		return resolved;
	}
	#endregion
}
