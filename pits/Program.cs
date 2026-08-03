using System.Reflection;
using System.Linq;
using JsonPit;
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
	public static string[] Help =>
	[
		$"-h, --help\t{Icons.Help}\tprint out all options",
		$"-v, --version\t{Icons.Info}\tprint version info",
		$"-n, --nologo\t{(Banner ? Icons.Banner : Icons.NoBanner)}\tdo not display the banner",
		$"-b, --debug\t{Icons.Info}\tenable debug output",
		$"-r, --pitroot\t{Icons.Folder}\t{PitRootDescription()}",
		$"-c, --cloud\t{Icons.Folder}\t{CloudDescription()}",
		$"-s, --source\t{Icons.File}\t{SourceDescription()}",
		$"-e, --export\t{Icons.Download}\t{ExportDescription()}",
		$"--json\t\t{(Json ? Icons.Success : Icons.NotAvailable)}\texport to stdout (for piping to jq, grep, etc.)",
		$"--wwwa\t\t{(Wwwa ? Icons.Success : Icons.NotAvailable)}\toperate on all 4 pits (Person, Object, Place, Activity)",
		$"--retain-window\t{Icons.Info}\tkeep this CLI process activity window until its normal timeout",
		$"{Icons.Info} PitName\t{Icons.File}\t{PitNameDescription()}",
		$"\t\t{Icons.Info}\tpositional arg: pit to operate on, or target pit name when used with -s",
		$"\t\t{Icons.Info}\te.g. 'pits -s patch.json5 -r <root> Activity' seeds Activity.pit from patch.json5",
		$"{Icons.Info} PitRoot\t{PitRootStatusLine()}",
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
		if (!string.IsNullOrWhiteSpace(CloudProvider))
		{
			var cloudDir = Os.Config?.Cloud?[CloudProvider];
			return $"{CloudProvider}: {cloudDir ?? "(not configured)"}";
		}
		return "cloud provider name (looks up root in Os.Config)";
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
	private static string PitRootStatusLine()
	{
		if (PitRoot == null)
			return $"{Icons.NotAvailable}\t(not set)";
		var exists = PitRoot.Exists();
		return $"{(exists ? Icons.Success : Icons.NotAvailable)}\t{PitRoot.FullPath}";
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
	public static void WriteLine(string text, char underlineChar = '=')
	{
		for (int i = 0; i < text.Length; i++) Console.Write(underlineChar);
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
		foreach (var line in Help) WriteSuccess(line);
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
			string? cloudProvider = Messages.CloudProvider = ParamValue(args, "-c", "--cloudprovider", "--cloud");
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
	#region Helpers for argument parsing
	private static readonly string[] SwitchesWithValues = { "-s", "--source", "-r", "--pitroot", "-e", "--export", "-c", "--cloudprovider", "--cloud" };
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
			lock (ActivePitsLock)
				ActivePits.Remove(pit);
			return;
		}
		try
		{
			if (pit.TryReleaseProcessWindow())
				Messages.WriteDebug($"Released process activity window for {pit.JsonFile.Name}.");
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
