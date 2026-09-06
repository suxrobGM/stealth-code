using Avalonia.Input;
using StealthCode.Services.Hotkeys;

namespace StealthCode.Controls;

/// <summary>Turns a keystroke into the string <c>HotkeyService</c> parses back out, spelled the way <c>HotkeyCodes</c> does.</summary>
public static class HotkeyFormatter
{
    public static bool IsModifier(Key key) => key
        is Key.LeftCtrl or Key.RightCtrl
        or Key.LeftAlt or Key.RightAlt
        or Key.LeftShift or Key.RightShift
        or Key.LWin or Key.RWin;

    /// <summary>Ordered so the same combination always produces the same string.</summary>
    public static List<string> Modifiers(KeyModifiers modifiers)
    {
        var parts = new List<string>(HotkeyCodes.Modifiers.Length);

        foreach (var (name, _) in HotkeyCodes.Modifiers)
        {
            if (AvaloniaModifier(name) is { } flag && modifiers.HasFlag(flag))
            {
                parts.Add(name);
            }
        }

        return parts;
    }

    /// <summary>Name that ParseHotkey expects, or null if the key cannot be bound.</summary>
    public static string? KeyName(Key key) => key switch
    {
        >= Key.A and <= Key.Z => HotkeyCodes.KeyName(key.ToString()),
        >= Key.D0 and <= Key.D9 => HotkeyCodes.KeyName(key.ToString()[1..]),
        >= Key.F1 and <= Key.F24 => HotkeyCodes.KeyName(key.ToString()),
        _ => NamedKey(key) is { } name ? HotkeyCodes.KeyName(name) : null
    };

    /// <summary>The flag Avalonia raises for a modifier name in the shared table.</summary>
    private static KeyModifiers? AvaloniaModifier(string name) => name switch
    {
        "Ctrl" => KeyModifiers.Control,
        "Alt" => KeyModifiers.Alt,
        "Shift" => KeyModifiers.Shift,
        "Win" => KeyModifiers.Meta,
        _ => null
    };

    /// <summary>
    /// Spelled out rather than taken from <see cref="Key.ToString"/>, which returns the older of the two names
    /// Avalonia gives keys such as Enter and PageUp.
    /// </summary>
    private static string? NamedKey(Key key) => key switch
    {
        Key.Space => "Space",
        Key.Enter => "Enter",
        Key.Tab => "Tab",
        Key.Escape => "Escape",
        Key.Back => "Backspace",
        Key.Delete => "Delete",
        Key.Insert => "Insert",
        Key.Home => "Home",
        Key.End => "End",
        Key.PageUp => "PageUp",
        Key.PageDown => "PageDown",
        Key.Up => "Up",
        Key.Down => "Down",
        Key.Left => "Left",
        Key.Right => "Right",
        Key.PrintScreen => "PrintScreen",
        _ => null
    };
}
