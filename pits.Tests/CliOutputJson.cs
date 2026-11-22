using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace PitSeeder.Tests;

internal static class CliOutputJson
{
	private static readonly JsonSerializerSettings Settings = new() { DateParseHandling = DateParseHandling.None };

	public static T Parse<T>(string output) where T : JToken =>
		JsonConvert.DeserializeObject<T>(output, Settings) ?? throw new JsonReaderException("Expected JSON output.");
}
