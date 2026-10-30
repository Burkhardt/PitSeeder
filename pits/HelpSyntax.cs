using OsLib;

internal static class HelpSyntax
{
	internal static string Format(string text, string baseColor, bool useColor)
		=> CliHelpStyle.Syntax(text, baseColor, useColor);
}
