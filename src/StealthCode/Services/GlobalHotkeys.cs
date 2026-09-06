using CommunityToolkit.Mvvm.Messaging;
using StealthCode.Messages;
using StealthCode.Models;

namespace StealthCode.Services;

/// <summary>
/// Binds the app-wide hotkeys and reports the ones Windows refused. A refusal means another application already
/// owns the combination, which otherwise looks identical to a working binding.
/// </summary>
public sealed class GlobalHotkeys(HotkeyService hotkeyService, SettingsService settingsService) : IDisposable
{
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
            .Where(binding => !Rebind(binding.Key, binding.Value))
            .Select(binding => binding.Value)
            .ToList();

        if (refused.Count > 0)
        {
            WeakReferenceMessenger.Default.Send(new ShowToastMessage(
                $"Hotkey already taken by another app: {string.Join(", ", refused)}", StatusLevel.Warning));
        }
    }

    /// <summary>Points a named binding at a new combination. False when Windows refused it.</summary>
    public bool Rebind(string name, string hotkey) =>
        actions.TryGetValue(name, out var callback)
        && hotkeyService.Register(name, hotkey, hwnd, callback);

    /// <summary>Unregisters every binding and restores the window procedure this class replaced.</summary>
    public void Dispose() => hotkeyService.Dispose();

    /// <summary>The saved combination for each binding this class owns. Audio registers its own.</summary>
    private Dictionary<string, string> SavedHotkeys()
    {
        var settings = settingsService.Settings;

        return new Dictionary<string, string>
        {
            ["capture"] = settings.Capture.Hotkey,
            ["multicapture"] = settings.Capture.MultiCaptureHotkey,
            ["opacity"] = settings.OpacityHotkey,
            ["nofocus"] = settings.NoFocusHotkey
        };
    }
}
