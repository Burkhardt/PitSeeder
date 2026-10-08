using System.Reflection;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
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
		$"Commands:\t{Icons.Info}\tlist (ls), seed, export, audit, delete-property, delete-item, maintain",
		$"  pits list -r <tenant|path> [-c <cloud>] [-a|--all] [-l|--long]",
		$"  pits seed <PitName> --source <file|-> [--require-existing|--patch]",
		$"  pits export (<PitName> | --wwwa) (--out-dir <dir> | --json) [--at <ISO-8601 timestamp>]",
		$"  pits audit <PitName> [--machine <all|local|name>] [--level <severity>] [--json]",
		$"  pits delete-property <PitName> <ItemId> <PropertyPath>",
		$"  pits delete-item <PitName> <ItemId>",
		$"  pits maintain (<PitName> | --wwwa) [--apply] [--archive-events] [--json]",
		$"-h, --help\t{Icons.Help}\tprint out all options",
		$"-v, --version\t{Icons.Info}\tprint version info",
		$"-n, --nologo\t{(Banner ? Icons.Banner : Icons.NoBanner)}\tdo not display the banner",
		$"-b, --debug\t{Icons.Info}\tenable debug output",
		$"-r, --pitroot\t{Icons.Folder}\t{PitRootDescription()}",
		$"-c, --cloud\t{CloudIcon()}\t{CloudDescription()}",
		$"-s, --source\t{Icons.File}\t{SourceDescription()}",
		$"-e, --export\t{Icons.Download}\t{ExportDescription()}",
		$"--json\t\t{(Json ? Icons.Success : Icons.NotAvailable)}\texport to stdout (for piping to jq, grep, etc.)",
		$"--at\t\t{Icons.Info}\tproject export history at an offset-explicit ISO-8601 timestamp",
		$"--wwwa\t\t{(Wwwa ? Icons.Success : Icons.NotAvailable)}\toperate on all 4 pits (Person, Object, Place, Activity)",
		$"--retain-window\t{Icons.Info}\t4.x compatibility: keep the activity window until timeout",
		$"--require-existing\t{Icons.Info}\tseed only: require every ID to exist in living state",
		$"--patch\t\t{Icons.Info}\talias for --require-existing",
		$"{Icons.Warning} Legacy\t{Icons.Info}\tflat seed/export flags remain supported in 4.x; use command syntax before 5.x",
		$"{Icons.Info} PitName\t{Icons.File}\t{PitNameDescription()}",
		$"\t\t{Icons.Info}\tpositional arg: pit to operate on, or target pit name when used with -s",
		$"\t\t{Icons.Info}\te.g. 'pits -s patch.json5 -r <root> Activity' seeds Activity from patch.json5",
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
				$"{CloudProviderIcon(name, index + 1)} {name}"))
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

		var pitDirectory = PitRoot / name;
		var displayFile = new PitFile(pitDirectory, name);
		if (!pitDirectory.Exists())
			return $"{Icons.NotAvailable}\t{displayFile.FullName}";
		try
		{
			using var pit = new Pit(pitDirectory, readOnly: true, unflagged: true);
			return $"{Icons.Success}\t{pit.JsonFile.FullName}";
		}
		catch
		{
			return $"{Icons.NotAvailable}\t{displayFile.FullName}";
		}
	}

	public static bool HasWwwaPit(RaiPath? pitRoot)
	{
		if (pitRoot == null) return false;
		return WwwaFiles.Any(name => IsValidPit(pitRoot / name));
	}

	private static bool IsValidPit(RaiPath pitDirectory)
	{
		if (!pitDirectory.Exists()) return false;
		try
		{
			using var pit = new Pit(pitDirectory, readOnly: true, unflagged: true);
			return pit.JsonFile.Exists();
		}
		catch
		{
			return false;
		}
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
	public static void WriteHelp(bool includeWwwaStatus = false)
	{
		foreach (var line in Help) WriteSuccess(line + Icons.HelpLineWidthCompensation);
		if (!includeWwwaStatus) return;

		foreach (var name in WwwaFiles)
		{
			var separator = name == "Place" ? "\t\t" : "\t";
			WriteSuccess($"{Icons.Info} {name}{separator}{WwwaPitStatus(name)}");
		}
	}
}

internal sealed class StrictPatchValidationException(string message) : Exception(message);

internal static class Program
{
	private const string CliSubscriber = "pits";
	private static readonly string[] Commands =
	[
		"list", "ls", "seed", "export", "audit", "delete-property", "delete-item", "maintain"
	];
	private static readonly object ActivePitsLock = new();
	private static readonly HashSet<Pit> ActivePits = [];
	static Program()
	{
		Console.CancelKeyPress += (_, _) => ReleaseAllProcessWindows();
		AppDomain.CurrentDomain.ProcessExit += (_, _) => ReleaseAllProcessWindows();
	}
	private static int Main(string[] args)
	{
		if (HasOption(args, "-v", "--version"))
			return RunMappedArguments(args);

		if (args.Length > 0 && Commands.Contains(args[0], StringComparer.Ordinal))
			return RunCommand(args[0], args[1..]);

		if (!HasOption(args, "-h", "--help") &&
			CliVerbDispatch.DetectMisplacedVerb("pits", args, Commands) is { } diagnostic)
		{
			Console.Error.WriteLine(diagnostic.Message);
			return 2;
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
			bool requireExisting = HasOption(args, "--require-existing", "--patch");
			Messages.RetainWindow = HasOption(args, "--retain-window");
			bool events = Messages.Events = HasOption(args, "--events");
			string? eventMachine = ParamValue(args, "--event-machine");
			string? eventLevel = ParamValue(args, "--event-level");
			var requestedCloudProvider = ParamValue(args, "-c", "--cloudprovider", "--cloud");
			string? cloudProvider = Messages.CloudProvider = ResolveCloudProvider(requestedCloudProvider);
			var pitRootParam = ParamValue(args, "-r", "--pitroot");
			var sourceParam = Messages.Source = ParamValue(args, "-s", "--source");
			var exportParam = Messages.Export = ParamValue(args, "-e", "--export");
			var atParam = ParamValue(args, "--at");
			if (HasOption(args, "--at") && string.IsNullOrWhiteSpace(atParam))
			{
				Messages.WriteError("--at requires a value with explicit Z or numeric UTC offset.");
				return 1;
			}
			var at = atParam is null ? (DateTimeOffset?)null : ParseProjectionTimestamp(atParam);
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
				if (at is not null)
				{
					Messages.WriteError("--at applies only to export.");
					return 1;
				}
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
				if (at is not null)
				{
					Messages.WriteError("--at applies only to export.");
					return 1;
				}
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
				if (at is not null)
				{
					Messages.WriteError("--at applies only to export.");
					return 1;
				}
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
				Messages.WriteDebug($"At: {at:O}");
			}
			if (showHelp || !hasExecutionIntent)
			{
				Messages.WriteHelp(wwwa || Messages.HasWwwaPit(pitRoot));
				if (!hasExecutionIntent) return showHelp ? 0 : 1;
			}
			#endregion
			#region REAL WORK EXECUTION
			// WWWA seed
			if (wwwa && !string.IsNullOrWhiteSpace(sourceParam) && !sourceParam.EndsWith(".pit", StringComparison.OrdinalIgnoreCase))
			{
				RequireExplicitMutationLocation(args, "seed");
				var sourceDir = new RaiPath(sourceParam.EndsWith(Os.DIR) ? sourceParam : sourceParam + Os.DIR);
				return RunBulkSeed(sourceDir, pitRoot!);
			}
			// WWWA export to file
			if (wwwa && !string.IsNullOrWhiteSpace(exportParam))
			{
				var exportPath = new RaiPath(exportParam);
				return ExportWwwa(pitRoot!, exportPath, at);
			}
			// WWWA export to stdout
			if (wwwa && json)
			{
				return ExportWwwaToStdout(pitRoot!, at);
			}
			// Single pit export to stdout via positional name
			if (!string.IsNullOrWhiteSpace(pitName) && json)
			{
				var pitFile = new PitFile(pitRoot! / pitName, pitName);
				return ExportPitToStdout(pitFile, at);
			}
			// Single pit export to file via positional name
			if (!string.IsNullOrWhiteSpace(pitName) && !string.IsNullOrWhiteSpace(exportParam))
			{
				var pitFile = new PitFile(pitRoot! / pitName, pitName);
				var exportPath = new RaiPath(exportParam);
				return ExportPitToFile(pitFile, exportPath, at);
			}
			// Single pit export via -s (pointing to .pit file)
			if (!string.IsNullOrWhiteSpace(sourceParam) && sourceParam.EndsWith(".pit", StringComparison.OrdinalIgnoreCase))
			{
				var pitFile = new PitFile(sourceParam);
				if (json)
					return ExportPitToStdout(pitFile, at);
				if (!string.IsNullOrWhiteSpace(exportParam))
					return ExportPitToFile(pitFile, new RaiPath(exportParam), at);
			}
			// Single seed: -s source.json5 [PitName] -r pitroot
			// Target pit name is the trailing positional arg if provided, else the source file name.
			if (!string.IsNullOrWhiteSpace(sourceParam) && pitRoot != null)
			{
				RequireExplicitMutationLocation(args, "seed");
				if (sourceParam == "-")
				{
					if (string.IsNullOrWhiteSpace(pitName)) throw new ArgumentException("Standard input requires an explicit PitName.");
					SeedPayload(Console.In.ReadToEnd(), "stdin", new PitFile(pitRoot / pitName, pitName), requireExisting);
					return 0;
				}
				var sourceFile = new TextFile(sourceParam);
				if (!sourceFile.Exists())
				{
					Messages.WriteError($"Source file '{sourceFile.FullName}' does not exist.");
					return 1;
				}
				var name = !string.IsNullOrWhiteSpace(pitName) ? pitName : sourceFile.Name;
				var pitFile = new PitFile(pitRoot / name, name);
				SeedPit(sourceFile, pitFile, requireExisting);
				return 0;
			}
			#endregion
		}
		catch (StrictPatchValidationException ex)
		{
			Console.Error.WriteLine($"error: {ex.Message}");
		}
		catch (JsonPitException ex)
		{
			Console.Error.WriteLine($"Error: {ex.Message}");
		}
		catch (ArgumentException ex)
		{
			Console.Error.WriteLine($"error: {ex.Message}");
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
				"list" or "ls" => RunListCommand(args),
				"seed" => RunSeedCommand(args),
				"export" => RunExportCommand(args),
				"audit" => RunAuditCommand(args),
				"delete-property" => RunDeletePropertyCommand(args),
				"delete-item" => RunDeleteItemCommand(args),
				"maintain" => RunMaintainCommand(args),
				_ => throw new ArgumentException($"Unknown command '{command}'.")
			};
		}
		catch (StrictPatchValidationException ex)
		{
			Console.Error.WriteLine($"error: {ex.Message}");
			return 1;
		}
		catch (JsonPitException ex)
		{
			Console.Error.WriteLine($"Error: {ex.Message}");
			return 1;
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

	private sealed record PitDiscovery(string? Provider, RaiPath Root, string[] Names);

	private static int RunListCommand(string[] args)
	{
		args = ExpandListOptionBundles(args);
		var valueOptions = new[] { "-r", "--pitroot", "-c", "--cloudprovider", "--cloud" }.ToHashSet(StringComparer.Ordinal);
		var allowed = valueOptions.Concat(["-n", "--nologo", "-b", "--debug", "-a", "--all", "-l", "--long"]).ToHashSet(StringComparer.Ordinal);
		var positionals = ValidateCommandTokens(args, allowed, valueOptions);
		if (positionals.Count != 0)
			throw new ArgumentException("list accepts no positional PitName; specify the tenant or local directory with -r or --pitroot.");

		var root = ParamValue(args, "-r", "--pitroot");
		if (string.IsNullOrWhiteSpace(root))
			throw new ArgumentException("list requires -r or --pitroot <tenant-or-path>.");
		var requestedCloud = ParamValue(args, "-c", "--cloudprovider", "--cloud");
		var all = HasOption(args, "-a", "--all");
		var longListing = HasOption(args, "-l", "--long");
		if (all && !string.IsNullOrWhiteSpace(requestedCloud))
			throw new ArgumentException("--all scans DefaultCloudOrder and cannot be combined with -c or --cloud.");

		if (!string.IsNullOrWhiteSpace(requestedCloud))
		{
			var provider = ResolveCloudProvider(requestedCloud)!;
			var discovery = new PitDiscovery(provider, ResolveCloudRoot(provider, root), DiscoverPitNames(ResolveCloudRoot(provider, root)));
			WriteDiscoveredPits(discovery, longListing);
			return 0;
		}

		if (IsExplicitLocalDirectory(root))
		{
			var local = new RaiPath(root);
			var names = DiscoverPitNames(local);
			Console.WriteLine($"Found {names.Length} pit(s) in local directory '{local.FullPath}':");
			foreach (var name in names) Console.WriteLine(FormatPitListing(local, name, longListing));
			return 0;
		}

		var discoveries = DiscoverTenantAcrossConfiguredClouds(root);
		if (all)
		{
			foreach (var discovery in discoveries) WriteAllDiscoveryStatus(discovery, longListing);
			return 0;
		}

		var found = discoveries.FirstOrDefault(discovery => discovery.Names.Length > 0);
		if (found is null)
		{
			Console.WriteLine($"No pits found under tenant root '{root}' across configured clouds.");
			return 0;
		}
		WriteDiscoveredPits(found, longListing);
		return 0;
	}

	internal static string[] ExpandListOptionBundles(IEnumerable<string> args)
		=> args.SelectMany(argument => argument switch
		{
			"-la" or "-al" => new[] { "-l", "-a" },
			_ => new[] { argument }
		}).ToArray();

	private static IReadOnlyList<PitDiscovery> DiscoverTenantAcrossConfiguredClouds(string tenant)
	{
		if (!Os.IsConfigLoaded) throw new ArgumentException(MissingConfigurationDiagnostic());
		var providers = Messages.CloudProviderOptions();
		if (providers.Length == 0)
			throw new ArgumentException("No configured DefaultCloudOrder providers are available for tenant discovery.");
		return providers.Select(provider =>
		{
			var root = ResolveCloudRoot(provider, tenant);
			return new PitDiscovery(provider, root, DiscoverPitNames(root));
		}).ToArray();
	}

	private static RaiPath ResolveCloudRoot(string provider, string? relativeRoot)
	{
		string? cloudDirectory = Os.Config?.Cloud?[provider];
		if (string.IsNullOrWhiteSpace(cloudDirectory))
			throw new ArgumentException($"The requested cloud provider '{provider}' is missing or empty in {Os.DefaultConfigFileLocation}.");
		var cloudRoot = new RaiPath(cloudDirectory);
		return string.IsNullOrWhiteSpace(relativeRoot)
			? cloudRoot
			: cloudRoot / new RaiRelPath(relativeRoot.TrimStart('/', '\\'));
	}

	private static string[] DiscoverPitNames(RaiPath root)
	{
		if (!root.Exists()) return [];

		return root.EnumerateDirectories("*")
			.OrderBy(directory => directory.FullPath, StringComparer.OrdinalIgnoreCase)
			.Select(TryOpenPit)
			.OfType<string>()
			.Distinct(StringComparer.OrdinalIgnoreCase)
			.Order(StringComparer.OrdinalIgnoreCase)
			.ToArray();
	}

	/// <summary>
	/// Opens a candidate through JsonPit rather than treating a file called *.pit as
	/// evidence of a usable Pit. The constructor validates the canonical Pit shape
	/// and loads it without taking a process flag.
	/// </summary>
	private static string? TryOpenPit(RaiPath directory)
	{
		try
		{
			using var pit = new Pit(directory, readOnly: true, unflagged: true);
			return pit.JsonFile.Exists() ? pit.JsonFile.Name : null;
		}
		catch
		{
			return null;
		}
	}

	private static void WriteDiscoveredPits(PitDiscovery discovery, bool longListing)
		=> Console.WriteLine(RenderPitDiscovery(discovery.Provider, discovery.Root.FullPath, discovery.Names, all: false, longListing, discovery.Root));

	private static void WriteAllDiscoveryStatus(PitDiscovery discovery, bool longListing)
		=> Console.WriteLine(RenderPitDiscovery(discovery.Provider, discovery.Root.FullPath, discovery.Names, all: true, longListing, discovery.Root));

	internal static string RenderPitDiscovery(string? provider, string resolvedPath, IEnumerable<string> names, bool all)
		=> RenderPitDiscovery(provider, resolvedPath, names, all, longListing: false, root: null);

	private static string RenderPitDiscovery(
		string? provider,
		string resolvedPath,
		IEnumerable<string> names,
		bool all,
		bool longListing,
		RaiPath? root)
	{
		var discovered = names.ToArray();
		if (all && discovered.Length == 0)
		{
			return $"[{provider}] ({resolvedPath}): No pits found.";
		}
		var heading = all
			? $"[{provider}] ({resolvedPath}): {discovered.Length} pit(s) found"
			: $"Found {discovered.Length} pit(s) in cloud '{provider}' ({resolvedPath}):";
		return string.Join(Environment.NewLine, [heading, .. discovered.Select(name => FormatPitListing(root, name, longListing))]);
	}

	private static string FormatPitListing(RaiPath? root, string name, bool longListing)
	{
		if (!longListing || root is null) return $"  {name}";
		var pitDirectory = root / name;
		using var pit = new Pit(pitDirectory, readOnly: true, unflagged: true);
		var file = pit.JsonFile;
		return $"  {name,-12}  {FormatPitSize(file.Length),9}   {file.LastWriteTimeUtc.LocalDateTime:yyyy-MM-dd HH:mm}";
	}

	private static string FormatPitSize(long bytes) => $"{bytes / 1024d:F1} KB";

	private static int RunSeedCommand(string[] args)
	{
		var valueOptions = GlobalValueOptions.Concat(["--source"]).ToHashSet(StringComparer.Ordinal);
		var allowed = GlobalSwitchOptions.Concat(valueOptions).Concat([
			"--wwwa", "--require-existing", "--patch"
		]).ToHashSet(StringComparer.Ordinal);
		var positionals = ValidateCommandTokens(args, allowed, valueOptions);
		var wwwa = HasOption(args, "--wwwa");
		var requireExisting = HasOption(args, "--require-existing", "--patch");
		var source = ParamValue(args, "--source");

		if (string.IsNullOrWhiteSpace(source))
			throw new ArgumentException("seed requires --source <file-or-directory|->.");
		if (wwwa && positionals.Count > 0)
			throw new ArgumentException("seed accepts either <PitName> or --wwwa, not both.");
		if (wwwa && requireExisting)
			throw new ArgumentException("--require-existing / --patch applies to single-pit seed operations, not --wwwa.");
		if (wwwa && source == "-")
			throw new ArgumentException("--source - applies to single-pit seed operations, not --wwwa.");
		if (!wwwa && positionals.Count != 1)
			throw new ArgumentException("seed requires exactly one <PitName>, or --wwwa for the four-pit source directory.");
		RequireExplicitMutationLocation(args, "seed");

		return RunMappedArguments(args);
	}

	private static int RunExportCommand(string[] args)
	{
		var valueOptions = GlobalValueOptions.Concat(["--out-dir", "--at"]).ToHashSet(StringComparer.Ordinal);
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

	private static int RunMaintainCommand(string[] args)
	{
		var valueOptions = GlobalValueOptions.Concat(["--older-than"]).ToHashSet(StringComparer.Ordinal);
		var allowed = GlobalSwitchOptions.Concat(valueOptions).Concat([
			"--wwwa", "--apply", "--json", "--prune-process-flags", "--repair-legacy-extensions", "--archive-events"
		]).ToHashSet(StringComparer.Ordinal);
		var positionals = ValidateCommandTokens(args, allowed, valueOptions);
		var wwwa = HasOption(args, "--wwwa");
		var apply = HasOption(args, "--apply");
		var json = HasOption(args, "--json");
		var prune = HasOption(args, "--prune-process-flags");
		var repair = HasOption(args, "--repair-legacy-extensions");
		var archiveEvents = HasOption(args, "--archive-events");
		var olderThanText = ParamValue(args, "--older-than");

		if (wwwa && positionals.Count > 0)
			throw new ArgumentException("maintain accepts either <PitName> or --wwwa, not both.");
		if (!wwwa && positionals.Count != 1)
			throw new ArgumentException("maintain requires exactly one <PitName>, or --wwwa.");
		if (prune && !apply)
			throw new ArgumentException("--prune-process-flags requires --apply.");
		if (prune && string.IsNullOrWhiteSpace(olderThanText))
			throw new ArgumentException("--prune-process-flags requires --older-than <duration>.");
		if (!prune && olderThanText is not null)
			throw new ArgumentException("--older-than applies only with --prune-process-flags.");
		if (repair && !apply)
			throw new ArgumentException("--repair-legacy-extensions requires --apply.");
		if (apply) RequireExplicitMutationLocation(args, "maintain");
		TimeSpan? olderThan = null;
		if (olderThanText is not null)
		{
			if (!TimeSpan.TryParse(olderThanText, CultureInfo.InvariantCulture, out var parsed) || parsed <= TimeSpan.Zero)
				throw new ArgumentException("--older-than requires a positive TimeSpan, for example 7.00:00:00.");
			olderThan = parsed;
		}

		Messages.Debug = HasOption(args, "-b", "--debug");
		Messages.Banner = !json && !HasOption(args, "-n", "--nologo");
		Messages.RetainWindow = HasOption(args, "--retain-window");
		var root = ResolveCommandPitRoot(args, wwwa ? "WWWA" : positionals[0]);
		Messages.PitRoot = root;
		Messages.Wwwa = wwwa;
		if (Messages.Banner) Messages.WriteBanner($"{Icons.Info} AfricaStage Pit Seeder CLI");

		var options = new PitMaintenanceOptions
		{
			Apply = apply,
			PruneProcessFlags = prune,
			OlderThan = olderThan,
			RepairLegacyExtensions = repair,
			ArchiveEvents = archiveEvents
		};
		var names = wwwa ? Messages.WwwaFiles : [positionals[0]];
		if (!root.Exists())
			throw new RaiPathNotFoundException(
				$"The maintenance root does not exist: {root.FullPath}",
				root.FullPath);
		var targets = names
			.Select(name => new
			{
				Name = name,
				Path = root / name,
				File = new RaiFile(root / name, name, "pit")
			})
			.ToList();
		if (!wwwa && !targets[0].File.Exists())
			throw new RaiPathNotFoundException(
				$"The requested pit does not exist: {targets[0].File.FullName}",
				targets[0].Path.FullPath);
		if (wwwa && targets.All(target => !target.File.Exists()))
			throw new RaiPathNotFoundException(
				$"The maintenance root contains none of the WWWA pits: {root.FullPath}",
				root.FullPath);
		var results = new List<PitMaintenanceResult>();
		foreach (var target in targets)
		{
			if (!target.File.Exists())
			{
				var missing = new PitMaintenanceResult(target.File.FullName, apply);
				missing.Deferred.Add($"Pit does not exist and was not created: {target.File.FullName}");
				results.Add(missing);
				continue;
			}
			var pit = TrackPit(new Pit(
				target.Path,
				subscriber: CliSubscriber,
				readOnly: !apply,
				undercover: true,
				autoload: false));
			try
			{
				if (pit.JsonFile.Exists()) pit.Load(undercover: true);
				results.Add(pit.Maintain(options));
			}
			finally
			{
				// Maintenance itself creates no new domain mutation. Suppress the normal
				// disposal recovery publication so cleanup cannot recreate retired changes.
				pit.ReadOnly = true;
				ReleaseProcessWindow(pit);
			}
		}

		if (json)
		{
			JToken document = wwwa
				? new JArray(results.Select(JObject.FromObject))
				: JObject.FromObject(results[0]);
			Console.WriteLine(document.ToString(Formatting.Indented));
		}
		else
		{
			foreach (var result in results)
			{
				Messages.WriteInfo(
					$"{result.PitFile}: changes {result.ChangeFilesObserved} observed/{result.ChangeFilesMerged} merged/{result.ChangeFilesRemoved} removed; " +
					$"receipts {result.ReceiptsCreated} created/{result.ReceiptsRemoved} removed; " +
					$"flags {result.ProcessFlagsActive} active/{result.ProcessFlagsExpired} expired/{result.ProcessFlagsPruned} pruned; " +
					$"legacy {result.LegacyArtifactsObserved} observed/{result.LegacyArtifactsRepaired} repaired; " +
					$"events {result.EventFilesObserved} observed/{result.EventFilesArchived} archived/{result.EventFilesRemoved} removed" +
					(string.IsNullOrWhiteSpace(result.EventArchiveName) ? "." : $" as {result.EventArchiveName}."));
				foreach (var deferred in result.Deferred) Messages.WriteInfo($"Deferred: {deferred}");
				foreach (var failure in result.Failures) Messages.WriteError($"Failed: {failure}");
			}
		}
		return results.All(result => result.Succeeded) ? 0 : 1;
	}

	private static RaiPath ResolveCommandPitRoot(string[] args, string target)
	{
		var requestedCloudProvider = ParamValue(args, "-c", "--cloudprovider", "--cloud");
		var cloudProvider = Messages.CloudProvider = ResolveCloudProvider(requestedCloudProvider);
		var pitRootParam = ParamValue(args, "-r", "--pitroot");
		if (!string.IsNullOrWhiteSpace(cloudProvider))
		{
			string? cloudDirectory = Os.Config?.Cloud?[cloudProvider];
			if (string.IsNullOrWhiteSpace(cloudDirectory))
				throw new ArgumentException(
					$"The requested cloud provider '{cloudProvider}' is missing or empty in {Os.DefaultConfigFileLocation}.");
			var cloudRoot = new RaiPath(cloudDirectory);
			return !string.IsNullOrWhiteSpace(pitRootParam)
				? cloudRoot / new RaiRelPath(pitRootParam.TrimStart('/', '\\'))
				: cloudRoot;
		}
		if (!string.IsNullOrWhiteSpace(pitRootParam)) return new RaiPath(pitRootParam);
		throw new ArgumentException(
			$"Cannot resolve maintenance target '{target}' without -r or --pitroot, or a configured -c or --cloud provider.");
	}

	private static void RequireExplicitMutationLocation(string[] args, string command)
	{
		var requestedCloud = ParamValue(args, "-c", "--cloudprovider", "--cloud");
		var root = ParamValue(args, "-r", "--pitroot");
		if (!string.IsNullOrWhiteSpace(requestedCloud) || string.IsNullOrWhiteSpace(root)) return;
		if (IsExplicitLocalDirectory(root)) return;
		throw new ArgumentException(
			$"{command} refuses to guess a cloud for tenant root '{root}'. Supply -c <provider> or an explicit local directory path.");
	}

	private static bool IsExplicitLocalDirectory(string root)
	{
		if (string.IsNullOrWhiteSpace(root)) return false;
		try
		{
			var localRoot = new RaiPath(root);
			if (localRoot.Exists()) return true;

			// A seed target may not exist yet. Retain that valid local use case only
			// when the caller supplied an explicitly qualified RaiPath, never for a
			// bare tenant name that would otherwise make the cloud choice ambiguous.
			var supplied = Os.NormSeperator(root.Trim());
			return supplied == "."
				|| supplied == "~"
				|| supplied.StartsWith(Os.DIR, StringComparison.Ordinal)
				|| supplied.StartsWith("." + Os.DIR, StringComparison.Ordinal)
				|| supplied.StartsWith("~" + Os.DIR, StringComparison.Ordinal)
				|| (Os.IsWindows && supplied.Length > 2 && supplied[1] == ':' &&
					supplied[2..].StartsWith(Os.DIR, StringComparison.Ordinal));
		}
		catch
		{
			return false;
		}
	}

	private static int RunDeleteMutation(
		string[] args,
		string pitName,
		string itemId,
		string? propertyPath)
	{
		if (string.IsNullOrWhiteSpace(pitName) || string.IsNullOrWhiteSpace(itemId))
			throw new ArgumentException("PitName and ItemId must be non-empty values.");
		RequireExplicitMutationLocation(args, propertyPath is null ? "delete-item" : "delete-property");

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
			if (i + 1 >= args.Length || (args[i + 1].StartsWith("-", StringComparison.Ordinal)
				&& !(token == "--source" && args[i + 1] == "-")))
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
		if (!Os.IsConfigLoaded)
			throw new ArgumentException(MissingConfigurationDiagnostic());

		return ResolveAllowedCloudProvider(requestedCloudProvider, Messages.CloudProviderOptions());
	}

	internal static string MissingConfigurationDiagnostic()
		=> $"RAIkeep configuration was not found at '{Os.DefaultConfigFileLocation}'. " +
			"Run 'amafu init' to detect cloud providers and create it.";

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
				"Usage: pits seed <PitName> --source <file|-> [--require-existing|--patch] [global options]",
				"       pits seed --wwwa --source <directory> [global options]",
				"Imports JSON/JSON5 into one pit or the four WWWA pits.",
				"--require-existing, --patch  reject missing or tombstoned IDs before opening the Pit for write."
			},
			"list" or "ls" => new[]
			{
				"Usage: pits list -r <tenant|path> [-c <cloud>] [-a|--all] [-l|--long]",
				"       pits ls -r <tenant|path> [-c <cloud>] [-a|--all] [-l|--long]",
				"Lists pits with their cloud and resolved-directory provenance. Without -c, a tenant name scans configured DefaultCloudOrder; a local path remains local.",
				"-a, --all reports every configured cloud, including clouds with no pits. -l, --long adds size and modification time; -la and -al combine both."
			},
			"export" => new[]
			{
				"Usage: pits export (<PitName> | --wwwa) (--out-dir <dir> | --json) [--at <ISO-8601 timestamp>] [global options]",
				"Exports one pit or a resolved WWWA projection to files or standard output.",
				"--at requires Z or a numeric UTC offset and emits the CR017 _export/data envelope."
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
			"maintain" => new[]
			{
				"Usage: pits maintain (<PitName> | --wwwa) [--apply] [--archive-events] [--json] [global options]",
				"       [--prune-process-flags --older-than <duration>] [--repair-legacy-extensions]",
				"--archive-events previews the immutable same-directory archive; add --apply to create it and retire validated loose copies.",
				"Reports restart-safe change/receipt cleanup; --apply performs only explicitly authorized maintenance."
			},
			_ => Array.Empty<string>()
		};
		foreach (var line in lines)
			Messages.WriteSuccess(line);
		Messages.WriteInfo("Global options: -r|--pitroot, -c|--cloud, -b|--debug, -n|--nologo, --retain-window");
	}
	#region Helpers for argument parsing
	private static readonly string[] SwitchesWithValues = { "-s", "--source", "-r", "--pitroot", "-e", "--export", "-c", "--cloudprovider", "--cloud", "--event-machine", "--event-level", "--at", "--older-than" };
	private static string? ParamValue(string[] options, params string[] aliases)
		=> aliases.Select(a => Array.IndexOf(options, a)).Where(i => i >= 0)
			.Select(i => i + 1 < options.Length && (!options[i + 1].StartsWith("-")
				|| (options[i] is "--source" or "-s" && options[i + 1] == "-"))
				? options[i + 1]
				: throw new ArgumentException($"The option '{options[i]}' requires a value."))
			.FirstOrDefault();
	private static bool HasOption(string[] options, params string[] aliases)
		=> aliases.Any(options.Contains);
	private static readonly Regex ProjectionTimestampPattern = new(
		@"^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}(?:\.\d{1,7})?(?:Z|[+-]\d{2}:\d{2})$",
		RegexOptions.CultureInvariant);
	internal static DateTimeOffset ParseProjectionTimestamp(string value)
	{
		var candidate = value?.Trim();
		if (string.IsNullOrWhiteSpace(candidate) ||
			!ProjectionTimestampPattern.IsMatch(candidate) ||
			!DateTimeOffset.TryParse(
				candidate,
				CultureInfo.InvariantCulture,
				DateTimeStyles.None,
				out var timestamp))
			throw new ArgumentException(
				"--at requires an ISO-8601 timestamp with explicit Z or numeric offset, " +
				"for example 2026-08-27T12:00:00Z or 2026-08-27T14:00:00+02:00.");
		return timestamp.ToUniversalTime();
	}
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
		var reads = directories
			.Select(directory => new { Directory = directory, Result = PitAudit.Inspect(directory, machineFilter, minLevel) })
			.ToList();
		var events = reads
			.SelectMany(read => read.Result.Events)
			.OrderBy(e => e.Machine, StringComparer.Ordinal)
			.ThenBy(e => e.UtcTime)
			.ThenBy(e => e.EventId, StringComparer.Ordinal)
			.ToList();
		var issues = reads
			.SelectMany(read => read.Result.Issues.Select(issue => $"{read.Directory.FullPath}: {issue}"))
			.ToList();
		if (json)
		{
			var array = new JArray(events.Select(e => e.Content));
			Console.WriteLine(array.ToString(Formatting.Indented));
			foreach (var issue in issues) Console.Error.WriteLine($"Audit warning: {issue}");
			return issues.Count == 0 ? 0 : 1;
		}
		if (events.Count == 0)
		{
			Messages.WriteInfo($"No matching events under {string.Join(", ", directories.Select(directory => directory.FullPath + OsLib.EventDirectory.Name))}.");
			foreach (var issue in issues) Messages.WriteError($"Audit warning: {issue}");
			return issues.Count == 0 ? 0 : 1;
		}
		foreach (var e in events)
			Console.WriteLine($"{e.Machine}\t{e.UtcTime:o}\t{e.Level}\t{e.Stage}\t{e.Message}\t({e.FileName})");
		foreach (var issue in issues) Messages.WriteError($"Audit warning: {issue}");
		return issues.Count == 0 ? 0 : 1;
	}
	#endregion
	#region Seeding Methods
	private static void SeedPit(TextFile source, PitFile pitFile, bool requireExisting = false)
	{
		Messages.WriteInfo($"Seeding pit from source file: {source.FullName} \n\tto destination: {pitFile.FullName}");
		SeedPayload(source.ReadAllText(), source.FullName, pitFile, requireExisting);
	}
	private static void SeedPayload(string payload, string sourceName, PitFile pitFile, bool requireExisting)
	{
		var root = ParseSeedPayload(payload, sourceName);
		var shapeDiagnostic =
			$"Source '{sourceName}' must be a JSON array of entities, a single entity object " +
			"with a non-empty 'Id', or a keyed map of entity objects.";
		JArray itemsArray = root switch
		{
			JArray arr => arr,
			JObject obj when HasNonEmptyStringId(obj) => new JArray(obj),
			// Keyed object map: { "Id1": { ... }, "Id2": { ... } } → take the values
			JObject obj when obj.Properties().All(property => property.Value is JObject) =>
				new JArray(obj.Properties().Select(property => property.Value)),
			_ => throw new ArgumentException(shapeDiagnostic)
		};
		foreach (var item in itemsArray)
		{
			if (item is not JObject itemObject)
				throw new ArgumentException($"{shapeDiagnostic} Arrays and keyed maps may contain only JSON objects.");
			if (!HasNonEmptyStringId(itemObject))
				throw new ArgumentException(
					$"Source '{sourceName}' contains an entity without a non-empty string 'Id'.");
			PitItem.ValidateClientPayload(itemObject);
		}
		if (requireExisting)
			ValidateStrictPatchTargets(pitFile, itemsArray);

		// Parsing and validation deliberately precede opening the destination Pit. A rejected
		// client payload therefore cannot create flags, directories, or partial state.
		var pit = TrackPit(new Pit(pitFile, subscriber: CliSubscriber, readOnly: false));
		try
		{
			Messages.WriteDebug($"{Icons.Info} Processing {pit.JsonFile.Name} Pit...");
			// Keep the validated JSON token types. Re-parsing through JArray.Parse
			// interprets ISO dates in ordinary fields using the machine's timezone.
			pit.AddItems(itemsArray.Cast<JObject>().Select(item => new PitItem(item)));
			pit.Save();
			Messages.WriteSuccess($"[pits] Successfully committed {itemsArray.Count} entity(ies) to Pit '{pitFile.Name}'.");
		}
		finally
		{
			ReleaseProcessWindow(pit);
		}
	}

	private static void ValidateStrictPatchTargets(PitFile pitFile, JArray itemsArray)
	{
		if (itemsArray.Count == 0)
			throw new StrictPatchValidationException("Patch source contained 0 entities.");

		// CR047: this is an intentionally unflagged, read-only projection. It loads the
		// canonical history and valid change fragments without acquiring a process window,
		// a master lease, or permission to persist maintenance results. The writable Pit is
		// constructed only after every incoming ID has passed this living-state check.
		using var livingState = new Pit(
			pitFile.Path,
			subscriber: CliSubscriber,
			readOnly: true,
			unflagged: true,
			autoload: true);
		foreach (var item in itemsArray.OfType<JObject>())
		{
			var id = item[nameof(PitItem.Id)]!.Value<string>()!;
			if (!livingState.Contains(id, withDeleted: false))
				throw new StrictPatchValidationException(
					$"Entity '{id}' does not exist in Pit '{pitFile.Name}'. " +
					"Use without --require-existing / --patch to allow creating new entities.");
		}
	}

	private static bool HasNonEmptyStringId(JObject item) =>
		item[nameof(PitItem.Id)] is JValue { Type: JTokenType.String } id &&
		!string.IsNullOrWhiteSpace(id.Value<string>());

	private static JToken ParseSeedPayload(string payload, string sourceName)
	{
		if (string.IsNullOrWhiteSpace(payload))
			throw new ArgumentException($"Seed source '{sourceName}' is empty.");
		try
		{
			// StringReader is deliberately an in-memory parser boundary. Source file I/O
			// remains on OsLib's TextFile boundary above.
			using var textReader = new StringReader(payload);
			using var jsonReader = new JsonTextReader(textReader)
			{
				DateParseHandling = DateParseHandling.None
			};
			return JToken.Load(jsonReader, new JsonLoadSettings
			{
				CommentHandling = CommentHandling.Ignore,
				DuplicatePropertyNameHandling = DuplicatePropertyNameHandling.Error,
				LineInfoHandling = LineInfoHandling.Load
			});
		}
		catch (JsonException exception)
		{
			throw new ArgumentException(
				$"Seed source '{sourceName}' is not valid supported JSON5: {exception.Message}",
				exception);
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
	private static int ExportPitToFile(PitFile pitFile, RaiPath exportPath, DateTimeOffset? at = null)
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
			var exportFile = new RaiFile(exportPath, pit.JsonFile.Name, "json");
			if (at is null)
				pit.ExportJson(exportFile);
			else
				WriteJson(exportFile, CreatePointInTimeEnvelope(ProjectPit(pit, at), at.Value));
			Messages.WriteSuccess($"{Icons.Success} Exported {pit.JsonFile.Name} to {exportFile.FullName}");
			return 0;
		}
		finally
		{
			ReleaseProcessWindow(pit);
		}
	}
	private static int ExportPitToStdout(PitFile pitFile, DateTimeOffset? at = null)
	{
		if (!pitFile.Exists())
		{
			Messages.WriteError($"Pit file '{pitFile.FullName}' does not exist.");
			return 1;
		}
		var pit = TrackPit(new Pit(pitFile, subscriber: CliSubscriber, readOnly: true));
		try
		{
			var items = ProjectPit(pit, at);
			JToken document = at is null ? items : CreatePointInTimeEnvelope(items, at.Value);
			Console.WriteLine(document.ToString(Formatting.Indented));
			return 0;
		}
		finally
		{
			ReleaseProcessWindow(pit);
		}
	}
	private static int ExportWwwa(RaiPath pitRoot, RaiPath exportPath, DateTimeOffset? at = null)
	{
		var resolved = BuildResolvedWwwa(pitRoot, at);
		if (resolved == null) return 1;
		exportPath.mkdir();
		var exportFile = new RaiFile(exportPath, "wwwa", "json");
		var document = at is null ? resolved : CreatePointInTimeEnvelope(resolved, at.Value);
		WriteJson(exportFile, document);
		Messages.WriteSuccess($"{Icons.Success} Exported resolved WWWA to {exportFile.FullName}");
		return 0;
	}
	private static int ExportWwwaToStdout(RaiPath pitRoot, DateTimeOffset? at = null)
	{
		var resolved = BuildResolvedWwwa(pitRoot, at);
		if (resolved == null) return 1;
		var document = at is null ? resolved : CreatePointInTimeEnvelope(resolved, at.Value);
		Console.WriteLine(document.ToString(Formatting.Indented));
		return 0;
	}
	private static JArray ProjectPit(Pit pit, DateTimeOffset? at)
	{
		var items = new JArray();
		foreach (var key in pit.Keys)
		{
			var item = at is null ? pit[key] : pit.GetAt(key, at.Value, withDeleted: false);
			if (item is not null)
				items.Add(item);
		}
		return items;
	}
	private static JObject CreatePointInTimeEnvelope(JToken data, DateTimeOffset at)
	{
		ArgumentNullException.ThrowIfNull(data);
		var exported = DateTimeOffset.UtcNow;
		return new JObject
		{
			["_export"] = new JObject
			{
				["at"] = at.UtcDateTime.ToString("O", CultureInfo.InvariantCulture),
				["exported"] = exported.UtcDateTime.ToString("O", CultureInfo.InvariantCulture)
			},
			["data"] = data
		};
	}
	private static void WriteJson(RaiFile exportFile, JToken document)
	{
		var textFile = new TextFile(exportFile.FullName)
		{
			Lines = [document.ToString(Formatting.Indented)],
			Changed = true
		};
		textFile.Save();
	}
	/// <summary>
	/// Builds the resolved WWWA export: loads all 4 pits, exports their items,
	/// and resolves one level of foreign key references (Who/What/Where/Activity).
	/// Resolved wrappers dissolve; unresolved wrappers remain.
	/// </summary>
	private static JObject? BuildResolvedWwwa(RaiPath pitRoot, DateTimeOffset? at = null)
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
					var item = at is null ? pit[key] : pit.GetAt(key, at.Value, withDeleted: false);
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
					var item = at is null
						? pits[name][key]
						: pits[name].GetAt(key, at.Value, withDeleted: false);
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
