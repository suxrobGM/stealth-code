using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;

namespace StealthCode.Controls;

/// <summary>Captures a hotkey by pressing it.</summary>
// ReSharper disable once PartialTypeWithSinglePart
public partial class HotkeyTextBox : UserControl
{
    public static readonly StyledProperty<string> HotkeyProperty =
        AvaloniaProperty.Register<HotkeyTextBox, string>(nameof(Hotkey), "");

    public HotkeyTextBox()
    {
        InitializeComponent();

        Input.IsReadOnly = true;
        Input.PlaceholderText = "Press a shortcut";
        Input.AddHandler(KeyDownEvent, OnInputKeyDown, RoutingStrategies.Tunnel);
        Input.GotFocus += (_, _) => SetHint("Press a shortcut, or Escape to cancel");
        Input.LostFocus += (_, _) => SetHint("");

        HotkeyProperty.Changed.AddClassHandler<HotkeyTextBox>((box, e) =>
        {
            var value = (string?)e.NewValue ?? "";
            if (box.Input.Text != value)
            {
                box.Input.Text = value;
            }
        });
    }

    public string Hotkey
    {
        get => GetValue(HotkeyProperty);
        set => SetValue(HotkeyProperty, value);
    }

    /// <summary>Tunnelled so keys like Tab and Escape are recorded instead of moving focus.</summary>
    private void OnInputKeyDown(object? sender, KeyEventArgs e)
    {
        e.Handled = true;

        if (e.Key == Key.Escape)
        {
            SetHint("");
            Focus();
            return;
        }

        // A modifier alone is not a combination.
        if (HotkeyFormatter.IsModifier(e.Key))
        {
            return;
        }

        var parts = HotkeyFormatter.Modifiers(e.KeyModifiers);
        if (parts.Count == 0)
        {
            SetHint("Add Ctrl, Alt, Shift or Win");
            return;
        }

        if (HotkeyFormatter.KeyName(e.Key) is not { } key)
        {
            SetHint("That key cannot be used in a shortcut");
            return;
        }

        parts.Add(key);
        Hotkey = string.Join("+", parts);
        SetHint("");
    }

    private void SetHint(string text)
    {
        ErrorText.Text = text;
        ErrorText.IsVisible = text.Length > 0;
    }
}
