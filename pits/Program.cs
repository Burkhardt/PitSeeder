using System;
using System.Linq;
using Newtonsoft.Json.Linq;
using JsonPit;
using OsLib;
using RaiUtils; // Assuming TextFile and RaiPath live in these namespaces

Console.WriteLine("========================================");
Console.WriteLine(" 🚀 AfricaStage Pit Seeder CLI");
Console.WriteLine("========================================");

// 1. CLI Argument Routing & Help
if (args.Length == 0 || args.Contains("-h") || args.Contains("--help"))
{
	Console.WriteLine("Usage:");
	Console.WriteLine("  Bulk Seed (Ontology Standard):");
	Console.WriteLine("    dotnet run -- -wwwa <SourceDirectoryPath>");
	Console.WriteLine("  Single Pit Seed:");
	Console.WriteLine("    dotnet run -- -pit <PitName> -file <SourceFile.json5>");
	return;
}

// 2. Resolve the OneDrive Cloud Storage Path using OsLib
var baseRoot = Os.CloudStorageRootDir / "Nomsa.net" / "AfricaStagePits";
Console.WriteLine($"📂 Target Storage Root: {baseRoot.Path}");

// 3. The "-wwwa" Bulk Seeder Logic
var wwwaIndex = Array.IndexOf(args, "-wwwa");
if (wwwaIndex >= 0 && args.Length > wwwaIndex + 1)
{
	var dirPath = args[wwwaIndex + 1];
	var sourceDir = new RaiPath(dirPath);

	Console.WriteLine($"\n🌊 Initiating WWWA Bulk Seed from: {sourceDir.Path}");

	// Process all four foundational pits
	SeedPit(new RaiFile(sourceDir, "Person.json5"), baseRoot, "Person");
	SeedPit(new RaiFile(sourceDir, "Place.json5"), baseRoot, "Place");
	SeedPit(new RaiFile(sourceDir, "Object.json5"), baseRoot, "Object");
	SeedPit(new RaiFile(sourceDir, "Activity.json5"), baseRoot, "Activity");

	Console.WriteLine("\n✅ Bulk seeding complete! Data is safe in the Pits.");
	return;
}

// 4. Single Pit Seeder Logic
var pitIndex = Array.IndexOf(args, "-pit");
var fileIndex = Array.IndexOf(args, "-file");

if (pitIndex >= 0 && fileIndex >= 0 && args.Length > pitIndex + 1 && args.Length > fileIndex + 1)
{
	var pitName = args[pitIndex + 1];
	var sourceFile = new RaiFile(args[fileIndex + 1]);

	Console.WriteLine($"\n🎯 Initiating Single Seed for: {pitName}");
	SeedPit(sourceFile, baseRoot, pitName);
	Console.WriteLine("\n✅ Single seed complete!");
}

// --- Core Seeding Method ---
static void SeedPit(RaiFile sourceFile, RaiPath baseRoot, string pitName)
{
	Console.WriteLine($"\n📦 Processing {pitName} Pit...");

	try
	{
		var textFile = new TextFile(sourceFile.FullName);
		string jsonContent = string.Join(Environment.NewLine, textFile.Lines);
		
		// Parse directly into a JArray
		var jsonArray = JArray.Parse(jsonContent);
		Console.WriteLine($"   -> Parsed {jsonArray.Count} items from {sourceFile.NameWithExtension}");

		// Define the target directory for this specific Pit
		var targetPitDir = (baseRoot / pitName).Path;

		// Use the specific UML Constructor: Pit(values: JArray, pitDirectory: string, autoload: bool, ...)
		// We set autoload: false so it doesn't attempt to hydrate from disk before we overwrite it with our clean seed data.
		var pit = new Pit(values: jsonArray, pitDirectory: targetPitDir, autoload: false, readOnly: false);

		// Persist the JArray directly to the CloudDrive
		pit.Save(force: true);

		Console.WriteLine($"   -> Successfully initialized and saved {pitName} to {targetPitDir}");
	}
	catch (Exception ex)
	{
		Console.WriteLine($"   [ERROR] Failed to process {pitName}. Details: {ex.Message}");
	}
}