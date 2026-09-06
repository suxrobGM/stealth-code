namespace StealthCode.Terminal.Web;

/// <summary>
/// Builds the terminal document. xterm's stylesheet and scripts are inlined into a single string so the
/// whole terminal can be handed to the WebView from memory — none of it is ever written to disk.
/// </summary>
public static class TerminalAssets
{
    private const string ResourcePrefix = "StealthCode.Terminal.Assets.";

    private static readonly string[] Scripts = ["xterm.min.js", "xterm-addon-fit.min.js", "terminal.js"];

    /// <summary>
    /// Origin the terminal document is served under. Nothing is ever fetched from it; the WebView only needs
    /// a base URI to resolve the document against, and an https one keeps the document a secure context so
    /// the clipboard APIs the terminal relies on stay available. The .invalid TLD cannot resolve, so a
    /// request that escapes the document goes nowhere instead of out to a real host.
    /// </summary>
    public static Uri BaseUri { get; } = new("https://terminal.stealthcode.invalid/");

    /// <summary>The whole terminal — markup, styles and scripts — as one self-contained document.</summary>
    public static string GetTerminalHtml(TerminalTheme theme)
    {
        var document = ReadResource("terminal.html");

        document = document.Replace(
            "<link rel=\"stylesheet\" href=\"xterm.css\">",
            $"<style>{Inline(ReadResource("xterm.css"))}</style>");

        foreach (var script in Scripts)
        {
            // terminal.js reads the theme at load, so it has to come first.
            var prelude = script == "terminal.js" ? ThemeScript(theme) : string.Empty;

            document = document.Replace(
                $"<script src=\"{script}\"></script>",
                $"{prelude}<script>{Inline(ReadResource(script))}</script>");
        }

        return document;
    }

    /// <summary>Colours terminal.js shares with the app chrome.</summary>
    private static string ThemeScript(TerminalTheme theme)
    {
        var fallback = TerminalTheme.Default;

        return "<script>window.__terminalTheme={"
            + $"background:{Quote(theme.Background, fallback.Background)},"
            + $"foreground:{Quote(theme.Foreground, fallback.Foreground)},"
            + $"panel:{Quote(theme.Panel, fallback.Panel)},"
            + $"border:{Quote(theme.Border, fallback.Border)},"
            + $"accent:{Quote(theme.Accent, fallback.Accent)},"
            + $"warning:{Quote(theme.Warning, fallback.Warning)},"
            + $"danger:{Quote(theme.Danger, fallback.Danger)}"
            + "};</script>";
    }

    /// <summary>Falls back to the default colour when the value is not a plain CSS one, since this is interpolated into a script.</summary>
    private static string Quote(string color, string fallback) =>
        color.All(c => char.IsAsciiLetterOrDigit(c) || c is '#' or '(' or ')' or ',' or '.' or '%' or ' ')
            ? $"\"{color}\""
            : $"\"{fallback}\"";

    /// <summary>
    /// Stops an asset that happens to contain a closing tag from ending the element it is being inlined into.
    /// Such a sequence only ever occurs inside a string literal, where both JavaScript and CSS read the
    /// backslash as an escape for the slash and the value is unchanged.
    /// </summary>
    private static string Inline(string content) => content
        .Replace("</script", "<\\/script", StringComparison.OrdinalIgnoreCase)
        .Replace("</style", "<\\/style", StringComparison.OrdinalIgnoreCase);

    private static string ReadResource(string fileName)
    {
        using var stream = typeof(TerminalAssets).Assembly.GetManifestResourceStream(ResourcePrefix + fileName)
                           ?? throw new InvalidOperationException($"Missing embedded terminal asset: {fileName}");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}
