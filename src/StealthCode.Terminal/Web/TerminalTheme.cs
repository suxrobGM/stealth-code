namespace StealthCode.Terminal.Web;

/// <summary>Colours the terminal document shares with the app chrome. The ANSI palette stays in terminal.js.</summary>
public sealed record TerminalTheme(
    string Background,
    string Foreground,
    string Panel,
    string Border,
    string Accent,
    string Warning,
    string Danger)
{
    public static TerminalTheme Default { get; } = new(
        "#1a1a1a", "#d4d4d4", "#252525", "#333333", "#10b981", "#f59e0b", "#ef4444");
}
