namespace StealthCode.Services.Hotkeys;

/// <summary>The one table of hotkey names: what a saved hotkey may spell and which Windows key or modifier it means.</summary>
public static class HotkeyCodes
{
    private const uint MOD_ALT = 0x0001;
    private const uint MOD_CONTROL = 0x0002;
    private const uint MOD_SHIFT = 0x0004;
    private const uint MOD_WIN = 0x0008;

    /// <summary>Modifier names in the order a saved hotkey lists them, with the flag each one passes to Windows.</summary>
    public static readonly (string Name, uint Flag)[] Modifiers =
    [
        ("Ctrl", MOD_CONTROL),
        ("Alt", MOD_ALT),
        ("Shift", MOD_SHIFT),
        ("Win", MOD_WIN)
    ];

    /// <summary>Named keys, with the shorthand spellings that are also accepted after the name that gets written out.</summary>
    private static readonly (string Name, uint Code)[] NamedKeys =
    [
        ("Space", 0x20),
        ("Enter", 0x0D),
        ("Return", 0x0D),
        ("Tab", 0x09),
        ("Escape", 0x1B),
        ("Esc", 0x1B),
        ("Backspace", 0x08),
        ("Back", 0x08),
        ("Delete", 0x2E),
        ("Del", 0x2E),
        ("Insert", 0x2D),
        ("Ins", 0x2D),
        ("Home", 0x24),
        ("End", 0x23),
        ("PageUp", 0x21),
        ("PgUp", 0x21),
        ("PageDown", 0x22),
        ("PgDn", 0x22),
        ("Up", 0x26),
        ("Down", 0x28),
        ("Left", 0x25),
        ("Right", 0x27),
        ("PrintScreen", 0x2C),
        ("PrtSc", 0x2C)
    ];

    /// <summary>The modifier flag a hotkey part sets, or zero when the part is not a modifier.</summary>
    public static uint ModifierFlag(string part)
    {
        foreach (var (name, flag) in Modifiers)
        {
            if (name.Equals(part, StringComparison.OrdinalIgnoreCase))
            {
                return flag;
            }
        }

        return 0;
    }

    /// <summary>The Windows key code a hotkey part means, or zero when it names no key that can be bound.</summary>
    public static uint KeyCode(string part)
    {
        if (part.Length == 1)
        {
            return char.ToUpperInvariant(part[0]);
        }

        if (part.StartsWith("F", StringComparison.OrdinalIgnoreCase)
            && int.TryParse(part.AsSpan(1), out var number)
            && number is >= 1 and <= 24)
        {
            return (uint)(0x6F + number);
        }

        foreach (var (name, code) in NamedKeys)
        {
            if (name.Equals(part, StringComparison.OrdinalIgnoreCase))
            {
                return code;
            }
        }

        return 0;
    }

    /// <summary>The spelling a saved hotkey uses for a key, taking any shorthand, or null when the key cannot be bound.</summary>
    public static string? KeyName(string part)
    {
        var wanted = KeyCode(part);
        if (wanted == 0)
        {
            return null;
        }

        foreach (var (name, code) in NamedKeys)
        {
            if (code == wanted)
            {
                return name;
            }
        }

        return part.ToUpperInvariant();
    }
}
