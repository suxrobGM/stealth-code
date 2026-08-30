namespace StealthCode.Terminal;

/// <summary>
/// Builds the terminal document. xterm's stylesheet and scripts are inlined into a single string so the
/// whole terminal can be handed to the WebView from memory — none of it is ever written to disk.
/// </summary>
public static class TerminalAssets
{
    private const string ResourcePrefix = "StealthCode.Terminal.Assets.";

    private static readonly string[] Scripts = ["xterm.min.js", "xterm-addon-fit.min.js", "terminal.js"];

    private static string? html;

    /// <summary>
    /// Origin the terminal document is served under. Nothing is ever fetched from it; the WebView only needs
    /// a base URI to resolve the document against, and an https one keeps the document a secure context so
    /// the clipboard APIs the terminal relies on stay available. The .invalid TLD cannot resolve, so a
    /// request that escapes the document goes nowhere instead of out to a real host.
    /// </summary>
    public static Uri BaseUri { get; } = new("https://terminal.stealthcode.invalid/");

    /// <summary>The whole terminal — markup, styles and scripts — as one self-contained document.</summary>
    public static string GetTerminalHtml() => html ??= BuildHtml();

    private static string BuildHtml()
    {
        var document = ReadResource("terminal.html");

        document = document.Replace(
            "<link rel=\"stylesheet\" href=\"xterm.css\">",
            $"<style>{Inline(ReadResource("xterm.css"))}</style>");

        foreach (var script in Scripts)
        {
            document = document.Replace(
                $"<script src=\"{script}\"></script>",
                $"<script>{Inline(ReadResource(script))}</script>");
        }

        return document;
    }

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
