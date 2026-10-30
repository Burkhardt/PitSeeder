using System.Text.RegularExpressions;

namespace PitSeeder.Tests;

public sealed class HelpFormattingTests
{
	[Fact]
	public void Options_AlignIconsAndDescriptionsWithoutTabs()
	{
		var lines = Messages.FormatHelp();
		Assert.All(lines, line => Assert.DoesNotContain("\t", line));
		Assert.All(lines, line => Assert.DoesNotContain("legacy", line, StringComparison.OrdinalIgnoreCase));
		Assert.DoesNotContain(lines, line => line.Contains("positional argument:", StringComparison.Ordinal));
		var options = lines.SkipWhile(line => line != "Options:").Skip(1).ToArray();
		Assert.NotEmpty(options);
		foreach (var line in options)
		{
			// Long/short labels and continuation rows share the same columns.
			Assert.Equal("  ", line.Substring(15, 2));
			Assert.False(char.IsWhiteSpace(line[17]));
			Assert.Equal("   ", line.Substring(18, 3));
			Assert.False(char.IsWhiteSpace(line[21]));
		}
	}

	[Fact]
	public void Colors_PreserveAlignmentAndApplyGlyphBaseColorWithUniformSwitchColor()
	{
		var plain = Messages.FormatHelp();
		var colored = Messages.FormatHelp(useColor: true);
		Assert.Equal(plain, colored.Select(Strip).ToArray());
		foreach (var (label, code) in new[]
		{
			("-h, --help", "32"), ("-r, --pitroot", "33"), ("-s, --source", "35"),
			("-e, --export", "34"), ("-n, --nologo", "95"), ("-x, --id-exists", "36")
		})
		{
			var row = Assert.Single(colored, line => Strip(line).StartsWith(label, StringComparison.Ordinal));
			var cells = Decode(row);
			Assert.Equal("32", cells[0].Color);
			Assert.Equal(code, cells[17].Color);
			Assert.Equal(code, cells[21].Color);
		}
	}

	[Fact]
	public void GlyphGroups_UseOneBaseColorPerGroupAndDistinctColorsForDifferentGroups()
	{
		var rows = Messages.FormatHelp(useColor: true)
			.SkipWhile(line => Strip(line) != "Options:").Skip(1);
		var groups = new Dictionary<char, string>();
		foreach (var row in rows)
		{
			var glyph = Strip(row)[17];
			var code = Decode(row)[17].Color;
			if (groups.TryGetValue(glyph, out var previous))
				Assert.Equal(previous, code);
			else
				groups.Add(glyph, code);
		}
		Assert.True(groups.Count >= 6);
		Assert.Equal(groups.Count, groups.Values.Distinct().Count());
	}

	[Fact]
	public void CloudOptions_MatchJpitBadgeColorsWithoutChangingTextOrAlignment()
	{
		string[] providers = ["OneDrive", "Dropbox", "GoogleDrive", "ICloudDrive"];
		var colored = Messages.FormatCloudOptions(providers, useColor: true);
		Assert.Contains($"\u001b[96m{Icons.OneDriveBoxOutline}\u001b[0m", colored, StringComparison.Ordinal);
		Assert.Contains($"\u001b[92m{Icons.DropboxBoxOutline}\u001b[0m", colored, StringComparison.Ordinal);
		Assert.Contains($"\u001b[94m{Icons.GoogleDriveBoxOutline}\u001b[0m", colored, StringComparison.Ordinal);
		Assert.Contains($"\u001b[91m{Icons.ICloudDriveBoxOutline}\u001b[0m", colored, StringComparison.Ordinal);
		foreach (var provider in providers)
			Assert.Contains($"\u001b[33m {provider}\u001b[0m", colored, StringComparison.Ordinal);
		Assert.Equal(Messages.FormatCloudOptions(providers, useColor: false),
			Regex.Replace(colored, @"\x1b\[[0-9;]*m", ""));

		var row = Assert.Single(Messages.FormatHelp(useColor: true),
			line => Strip(line).StartsWith("-c, --cloud", StringComparison.Ordinal));
		Assert.Contains("\u001b[33m", row, StringComparison.Ordinal);
		Assert.DoesNotContain("\t", row);
	}

	[Fact]
	public void Syntax_StylesRequiredValuesChoicesAndOptionalGroupsWithoutStylingAngleDelimiters()
	{
		const string text = "<tenant|path> (<PitName>|--wwwa) [-c <cloud>] [-a|--all]";
		var colored = HelpSyntax.Format(text, "36", useColor: true);
		Assert.Equal(text, Strip(colored));
		Assert.Equal(text, HelpSyntax.Format(text, "36", useColor: false));
		var cells = Decode(colored);
		Assert.Equal("96", cells[text.IndexOf("tenant", StringComparison.Ordinal)].Color);
		Assert.Equal("35", cells[text.IndexOf("PitName", StringComparison.Ordinal)].Color);
		Assert.Equal("94", cells[text.IndexOf('[')].Color);
		Assert.Equal("96", cells[text.IndexOf("cloud", StringComparison.Ordinal)].Color);
		Assert.Equal("32", cells[text.IndexOf("--wwwa", StringComparison.Ordinal)].Color);
		Assert.Equal("32", cells[text.IndexOf("--all", StringComparison.Ordinal)].Color);
		for (int i = 0; i < text.Length; i++)
		{
			if (text[i] is '<' or '>' or '|')
			{
				Assert.Equal("36", cells[i].Color);
				Assert.False(cells[i].Italic);
			}
		}
		Assert.True(cells[text.IndexOf("tenant", StringComparison.Ordinal)].Italic);
		Assert.True(cells[text.IndexOf("PitName", StringComparison.Ordinal)].Italic);
		Assert.True(cells[text.IndexOf("cloud", StringComparison.Ordinal)].Italic);
		Assert.False(cells[text.IndexOf("--all", StringComparison.Ordinal)].Italic);
	}

	[Fact]
	public void QuotedCode_KeepsSwitchColorsAndRestoresSurroundingTextStyle()
	{
		const string text = "e.g. 'pits seed Activity --source patch.json5 -r <root>' seeds Activity";
		var cells = Decode(HelpSyntax.Format(text, "36", useColor: true));
		Assert.Equal("33", cells[text.IndexOf("pits", StringComparison.Ordinal)].Color);
		Assert.Equal("32", cells[text.IndexOf("--source", StringComparison.Ordinal)].Color);
		Assert.Equal("32", cells[text.IndexOf("-r", StringComparison.Ordinal)].Color);
		Assert.Equal("96", cells[text.IndexOf("root", StringComparison.Ordinal)].Color);
		Assert.True(cells[text.IndexOf("root", StringComparison.Ordinal)].Italic);
		Assert.Equal("36", cells[text.LastIndexOf("seeds", StringComparison.Ordinal)].Color);
		Assert.False(cells[^1].Italic);
	}

	[Fact]
	public void ProseParentheses_AreNotTreatedAsOneOfGroups()
	{
		const string text = "(for piping to jq, grep, etc.)";
		Assert.All(Decode(HelpSyntax.Format(text, "36", useColor: true)), cell =>
		{
			Assert.Equal("36", cell.Color);
			Assert.False(cell.Italic);
		});
	}

	[Fact]
	public void Banner_StylesTheEntireHeadingBoldAndPreservesPlainOutput()
	{
		const string title = "Pit Seeder CLI (pits v4.5.14)";
		var colored = Messages.FormatBanner(title, useColor: true);
		Assert.Equal(Messages.FormatBanner(title, useColor: false), colored.Select(Strip).ToArray());
		Assert.All(Decode(colored[1]), cell => Assert.True(cell.Bold));
		Assert.All(Decode(colored[0]), cell => Assert.False(cell.Bold));
		var longTitle = $"{Icons.Info} " + new string('x', 80);
		Assert.Equal(longTitle.Length, Strip(Messages.FormatBanner(longTitle, useColor: true)[0]).Length);
	}

	private static string Strip(string text) => Regex.Replace(text, @"\x1b\[[0-9;]*m", "");
	private sealed record Cell(string Color, bool Italic, bool Bold);

	// Interpret emitted SGR sequences so assertions check the style actually
	// active at a printed character, including resets at nested boundaries.
	private static List<Cell> Decode(string styled)
	{
		var cells = new List<Cell>();
		string color = "0";
		bool italic = false, bold = false;
		foreach (Match match in Regex.Matches(styled, @"\x1b\[(?<codes>[0-9;]+)m|(?<text>[^\x1b]+)"))
		{
			if (match.Groups["codes"].Success)
			{
				foreach (var code in match.Groups["codes"].Value.Split(';'))
				{
					if (code == "0") { color = "0"; italic = false; bold = false; }
					else if (code == "1") bold = true;
					else if (code == "3") italic = true;
					else color = code;
				}
			}
			else
				foreach (var character in match.Groups["text"].Value)
					cells.Add(new(color, italic, bold));
		}
		return cells;
	}
}
