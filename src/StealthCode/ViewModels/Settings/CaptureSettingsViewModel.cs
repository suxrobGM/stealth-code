using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using StealthCode.Messages;
using StealthCode.ScreenCapture.Models;
using StealthCode.Services;

namespace StealthCode.ViewModels.Settings;

/// <summary>Screenshot capture mode, its target, hotkeys, and prompt.</summary>
public sealed partial class CaptureSettingsViewModel(
    SettingsService settingsService,
    CliProviderRegistry providerRegistry) : SettingsSectionViewModel(settingsService),
    IRecipient<RegionSelectedMessage>,
    IRecipient<WindowSelectedMessage>
{
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsRegionMode))]
    [NotifyPropertyChangedFor(nameof(IsWindowMode))]
    public partial CaptureMode SelectedMode { get; set; }

    [ObservableProperty]
    public partial string RegionDisplayText { get; set; } = "";

    [ObservableProperty]
    public partial string SelectedWindowTitle { get; set; } = "";

    [ObservableProperty]
    public partial string Hotkey { get; set; } = "Ctrl+Shift+C";

    [ObservableProperty]
    public partial string MultiCaptureHotkey { get; set; } = "Ctrl+Shift+X";

    [ObservableProperty]
    public partial string SystemPrompt { get; set; } = "";

    public bool IsRegionMode => SelectedMode == CaptureMode.Region;

    public bool IsWindowMode => SelectedMode == CaptureMode.Window;

    public override void Unload()
    {
        WeakReferenceMessenger.Default.Unregister<RegionSelectedMessage>(this);
        WeakReferenceMessenger.Default.Unregister<WindowSelectedMessage>(this);
    }

    public void Receive(RegionSelectedMessage message)
    {
        var capture = SettingsService.Settings.Capture;
        capture.RegionX = message.X;
        capture.RegionY = message.Y;
        capture.RegionWidth = message.Width;
        capture.RegionHeight = message.Height;
        RegionDisplayText = FormatRegion(message.X, message.Y, message.Width, message.Height);
        Save();
    }

    public void Receive(WindowSelectedMessage message)
    {
        var capture = SettingsService.Settings.Capture;
        capture.WindowHandle = message.Handle;
        capture.WindowTitle = message.Title;
        SelectedWindowTitle = message.Title;
        Save();
    }

    protected override void LoadCore()
    {
        var capture = SettingsService.Settings.Capture;

        SelectedMode = capture.Mode;
        RegionDisplayText = capture is { RegionWidth: > 0, RegionHeight: > 0 }
            ? FormatRegion(capture.RegionX, capture.RegionY, capture.RegionWidth, capture.RegionHeight)
            : "";
        SelectedWindowTitle = capture.WindowTitle;
        Hotkey = capture.Hotkey;
        MultiCaptureHotkey = capture.MultiCaptureHotkey;
        SystemPrompt = capture.SystemPrompt;

        WeakReferenceMessenger.Default.Register<RegionSelectedMessage>(this);
        WeakReferenceMessenger.Default.Register<WindowSelectedMessage>(this);
    }

    private static string FormatRegion(int x, int y, int width, int height) =>
        $"{width}×{height} at ({x}, {y})";

    [RelayCommand]
    private void SelectRegion() =>
        WeakReferenceMessenger.Default.Send(new RequestRegionSelectionMessage());

    [RelayCommand]
    private void SelectWindow() =>
        WeakReferenceMessenger.Default.Send(new RequestWindowSelectionMessage());

    [RelayCommand]
    private void ResetPrompt() => SystemPrompt = providerRegistry.GetActiveProvider().DefaultSystemPrompt;

    partial void OnSelectedModeChanged(CaptureMode value)
    {
        SettingsService.Settings.Capture.Mode = value;
        Save();
    }

    partial void OnHotkeyChanged(string value)
    {
        SettingsService.Settings.Capture.Hotkey = value;
        SaveHotkey("capture", value);
    }

    partial void OnMultiCaptureHotkeyChanged(string value)
    {
        SettingsService.Settings.Capture.MultiCaptureHotkey = value;
        SaveHotkey("multicapture", value);
    }

    partial void OnSystemPromptChanged(string value)
    {
        SettingsService.Settings.Capture.SystemPrompt = value;
        Save();
    }
}
