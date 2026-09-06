using CommunityToolkit.Mvvm.Messaging;
using StealthCode.Messages;
using StealthCode.Models;

namespace StealthCode.Services.Hotkeys;

/// <summary>
/// Binds the app-wide hotkeys and reports the ones Windows refused. A refusal means another application already
/// owns the combination, which otherwise looks identical to a working binding.
/// </summary>
public sealed class GlobalHotkeys(HotkeyService hotkeyService, SettingsService settingsService) : IDisposable
{
    private readonly HashSet<string> turnedOff = [];
    private IntPtr hwnd;
    private IReadOnlyDictionary<string, Action> actions = new Dictionary<string, Action>();

    public void Bind(IntPtr windowHandle, IReadOnlyDictionary<string, Action> handlers)
    {
        hwnd = windowHandle;
        actions = handlers;

        if (!OperatingSystem.IsWindows() || hwnd == IntPtr.Zero)
        {
            return;
        }

        var refused = SavedHotkeys()
            .Where(binding => !turnedOff.Contains(binding.Key) && !Rebind(binding.Key, binding.Value))
            .Select(binding => binding.Value)
            .ToList();

        ReportRefused(refused);
    }

    /// <summary>Points a named binding at a new combination. False when the binding is off or Windows refused it.</summary>
    public bool Rebind(string name, string hotkey) =>
        !turnedOff.Contains(name)
        && actions.TryGetValue(name, out var callback)
        && hotkeyService.Register(name, hotkey, hwnd, callback);

    /// <summary>Turns one binding on or off. A binding that is off stays unregistered until it is turned back on.</summary>
    public void SetEnabled(string name, bool enabled)
    {
        if (!enabled)
        {
            turnedOff.Add(name);
            hotkeyService.Unregister(name);
            return;
        }

        turnedOff.Remove(name);

        if (hwnd != IntPtr.Zero && SavedHotkeys().TryGetValue(name, out var hotkey) && !Rebind(name, hotkey))
        {
            ReportRefused([hotkey]);
        }
    }

    /// <summary>Unregisters every binding and restores the window procedure this class replaced.</summary>
    public void Dispose() => hotkeyService.Dispose();

    private static void ReportRefused(IReadOnlyList<string> hotkeys)
    {
        if (hotkeys.Count == 0)
        {
            return;
        }

        WeakReferenceMessenger.Default.Send(new ShowToastMessage(
            $"Hotkey already taken by another app: {string.Join(", ", hotkeys)}", StatusLevel.Warning));
    }

    /// <summary>The saved combination for each binding this class owns.</summary>
    private Dictionary<string, string> SavedHotkeys()
    {
        var settings = settingsService.Settings;

        return new Dictionary<string, string>
        {
            ["capture"] = settings.Capture.Hotkey,
            ["multicapture"] = settings.Capture.MultiCaptureHotkey,
            ["audio"] = settings.Audio.Hotkey,
            ["opacity"] = settings.OpacityHotkey,
            ["nofocus"] = settings.NoFocusHotkey
        };
    }
}
