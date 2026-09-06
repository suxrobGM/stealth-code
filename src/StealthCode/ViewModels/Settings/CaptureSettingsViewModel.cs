using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using StealthCode.Messages;
using StealthCode.ScreenCapture.Models;
using StealthCode.Services;
using StealthCode.Utilities;

namespace StealthCode.ViewModels.Settings;

/// <summary>Screenshot capture mode, its target, and the two prompts. Hotkeys live in General.</summary>
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
    [NotifyPropertyChangedFor(nameof(SystemPromptPreview))]
    public partial string SystemPrompt { get; set; } = "";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(MultiCaptureSystemPromptPreview))]
    public partial string MultiCaptureSystemPrompt { get; set; } = "";

    // Folded away by default; they are the tallest control here.
    [ObservableProperty]
    public partial bool IsPromptExpanded { get; set; }

    [ObservableProperty]
    public partial bool IsMultiCapturePromptExpanded { get; set; }

    public string SystemPromptPreview => PromptPreview.Of(SystemPrompt);

    public string MultiCaptureSystemPromptPreview => PromptPreview.Of(MultiCaptureSystemPrompt);

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
        SystemPrompt = capture.SystemPrompt;
        MultiCaptureSystemPrompt = capture.MultiCaptureSystemPrompt;

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

    [RelayCommand]
    private void ResetMultiCapturePrompt() =>
        MultiCaptureSystemPrompt = new CaptureSettings().MultiCaptureSystemPrompt;

    [RelayCommand]
    private void TogglePrompt() => IsPromptExpanded = !IsPromptExpanded;

    [RelayCommand]
    private void ToggleMultiCapturePrompt() => IsMultiCapturePromptExpanded = !IsMultiCapturePromptExpanded;

    partial void OnSelectedModeChanged(CaptureMode value)
    {
        SettingsService.Settings.Capture.Mode = value;
        Save();
    }

    partial void OnSystemPromptChanged(string value)
    {
        SettingsService.Settings.Capture.SystemPrompt = value;
        Save();
    }

    partial void OnMultiCaptureSystemPromptChanged(string value)
    {
        SettingsService.Settings.Capture.MultiCaptureSystemPrompt = value;
        Save();
    }
}
