using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using StealthCode.Messages;
using StealthCode.Services;

namespace StealthCode.ViewModels.Settings;

/// <summary>Window opacity and every hotkey. The five bindings share one global namespace.</summary>
public sealed partial class GeneralSettingsViewModel(SettingsService settingsService)
    : SettingsSectionViewModel(settingsService)
{
    /// <summary>The values the opacity hotkey cycles.</summary>
    public static IReadOnlyList<double> OpacityPresets { get; } = [1.0, 0.8, 0.6, 0.4];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsFullOpacity))]
    [NotifyPropertyChangedFor(nameof(IsHighOpacity))]
    [NotifyPropertyChangedFor(nameof(IsMediumOpacity))]
    [NotifyPropertyChangedFor(nameof(IsLowOpacity))]
    public partial double Opacity { get; set; } = 1.0;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HotkeyConflict))]
    public partial string CaptureHotkey { get; set; } = "Ctrl+Shift+C";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HotkeyConflict))]
    public partial string MultiCaptureHotkey { get; set; } = "Ctrl+Shift+X";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HotkeyConflict))]
    public partial string AudioHotkey { get; set; } = "Ctrl+Shift+A";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HotkeyConflict))]
    public partial string OpacityHotkey { get; set; } = "Ctrl+Shift+O";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HotkeyConflict))]
    public partial string NoFocusHotkey { get; set; } = "Ctrl+Shift+F";

    public bool IsFullOpacity => IsPreset(0);
    public bool IsHighOpacity => IsPreset(1);
    public bool IsMediumOpacity => IsPreset(2);
    public bool IsLowOpacity => IsPreset(3);

    /// <summary>Names a combination bound twice; the losing registration silently never fires.</summary>
    public string HotkeyConflict
    {
        get
        {
            var duplicated = new[] { CaptureHotkey, MultiCaptureHotkey, AudioHotkey, OpacityHotkey, NoFocusHotkey }
                .Where(h => !string.IsNullOrWhiteSpace(h))
                .GroupBy(h => h, StringComparer.OrdinalIgnoreCase)
                .Where(g => g.Count() > 1)
                .Select(g => g.Key)
                .ToList();

            return duplicated.Count > 0
                ? $"{string.Join(", ", duplicated)} is bound more than once"
                : "";
        }
    }

    public bool HasHotkeyConflict => HotkeyConflict.Length > 0;

    protected override void LoadCore()
    {
        var settings = SettingsService.Settings;

        Opacity = settings.WindowOpacity;
        CaptureHotkey = settings.Capture.Hotkey;
        MultiCaptureHotkey = settings.Capture.MultiCaptureHotkey;
        AudioHotkey = settings.Audio.Hotkey;
        OpacityHotkey = settings.OpacityHotkey;
        NoFocusHotkey = settings.NoFocusHotkey;
    }

    [RelayCommand]
    private void SetOpacity(double value) => Opacity = value;

    private bool IsPreset(int index) => Math.Abs(Opacity - OpacityPresets[index]) < 0.001;

    partial void OnOpacityChanged(double value)
    {
        if (IsLoading)
        {
            return;
        }

        SettingsService.Settings.WindowOpacity = value;
        WeakReferenceMessenger.Default.Send(new OpacityChangedMessage(value));
        Save();
    }

    partial void OnCaptureHotkeyChanged(string value)
    {
        SettingsService.Settings.Capture.Hotkey = value;
        SaveHotkey("capture", value);
        OnPropertyChanged(nameof(HasHotkeyConflict));
    }

    partial void OnMultiCaptureHotkeyChanged(string value)
    {
        SettingsService.Settings.Capture.MultiCaptureHotkey = value;
        SaveHotkey("multicapture", value);
        OnPropertyChanged(nameof(HasHotkeyConflict));
    }

    partial void OnAudioHotkeyChanged(string value)
    {
        SettingsService.Settings.Audio.Hotkey = value;
        SaveHotkey("audio", value);
        OnPropertyChanged(nameof(HasHotkeyConflict));
    }

    partial void OnOpacityHotkeyChanged(string value)
    {
        SettingsService.Settings.OpacityHotkey = value;
        SaveHotkey("opacity", value);
        OnPropertyChanged(nameof(HasHotkeyConflict));
    }

    partial void OnNoFocusHotkeyChanged(string value)
    {
        SettingsService.Settings.NoFocusHotkey = value;
        SaveHotkey("nofocus", value);
        OnPropertyChanged(nameof(HasHotkeyConflict));
    }
}
