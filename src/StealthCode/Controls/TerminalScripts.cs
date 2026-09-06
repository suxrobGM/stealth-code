using System.Text.Json;
using System.Text.Json.Serialization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using StealthCode.Models;
using StealthCode.Terminal;

namespace StealthCode.Controls;

/// <summary>Builds the JavaScript calls the terminal document exposes.</summary>
public static class TerminalScripts
{
    public static string Reset() => "termReset()";

    public static string Write(byte[] data) => $"termWrite('{Convert.ToBase64String(data)}')";

    public static string Ready() => "sendMessage({ type: 'ready', cols: term.cols, rows: term.rows })";

    /// <summary>Base64 like PTY output: the text is untrusted and must not be interpolated raw.</summary>
    public static string Toast(string message, StatusLevel level)
    {
        var payload = JsonSerializer.SerializeToUtf8Bytes(
            new ToastPayload(message, level.ToString().ToLowerInvariant()),
            TerminalJsonContext.Default.ToastPayload);

        return $"termToast('{Convert.ToBase64String(payload)}')";
    }

    /// <summary>Reads the terminal's chrome colours from Theme.axaml.</summary>
    public static TerminalTheme ResolveTheme() => new(
        Color("WindowBg") ?? TerminalTheme.Default.Background,
        Color("PrimaryFg") ?? TerminalTheme.Default.Foreground,
        Color("PanelBg") ?? TerminalTheme.Default.Panel,
        Color("DividerBrush") ?? TerminalTheme.Default.Border,
        Color("AccentBrush") ?? TerminalTheme.Default.Accent,
        Color("WarningFg") ?? TerminalTheme.Default.Warning,
        Color("DangerBrush") ?? TerminalTheme.Default.Danger);

    private static string? Color(string key) =>
        Application.Current?.TryFindResource(key, out var value) == true && value is ISolidColorBrush brush
            ? $"#{brush.Color.R:X2}{brush.Color.G:X2}{brush.Color.B:X2}"
            : null;
}

/// <summary>Shape handed to termToast in terminal.js.</summary>
public sealed record ToastPayload(string Text, string Level);

/// <summary>Source-generated: reflection-based serialization does not survive trimming.</summary>
[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(ToastPayload))]
internal sealed partial class TerminalJsonContext : JsonSerializerContext;
