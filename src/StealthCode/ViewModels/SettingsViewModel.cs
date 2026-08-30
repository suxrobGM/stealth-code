using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using StealthCode.Audio.Models;
using StealthCode.Audio.Services;
using StealthCode.Messages;
using StealthCode.ScreenCapture.Models;
using StealthCode.Services;
using StealthCode.Updater.Models;
using StealthCode.Updater.Services;

namespace StealthCode.ViewModels;

public sealed partial class SettingsViewModel(
    SettingsService settingsService,
    CliProviderRegistry providerRegistry,
    UpdateService updateService) : ViewModelBase,
    IRecipient<RegionSelectedMessage>,
    IRecipient<WindowSelectedMessage>
{
    private GitHubRelease? pendingRelease;

    [ObservableProperty]
    public partial IReadOnlyList<string> ProviderNames { get; set; } = [];

    [ObservableProperty]
    public partial int SelectedProviderIndex { get; set; }

    [ObservableProperty]
    public partial double Opacity { get; set; } = 1.0;

    [ObservableProperty]
    public partial string OpacityValueText { get; set; } = "100%";

    [ObservableProperty]
    public partial int SelectedCaptureModeIndex { get; set; }

    [ObservableProperty]
    public partial string Hotkey { get; set; } = "Ctrl+Shift+C";

    [ObservableProperty]
    public partial string MultiCaptureHotkey { get; set; } = "Ctrl+Shift+M";

    [ObservableProperty]
    public partial string OpacityHotkey { get; set; } = "Ctrl+Shift+O";

    [ObservableProperty]
    public partial string NoFocusHotkey { get; set; } = "Ctrl+Shift+F";

    [ObservableProperty]
    public partial string SystemPrompt { get; set; } = "";

    [ObservableProperty]
    public partial string AudioHotkey { get; set; } = "Ctrl+Shift+A";

    [ObservableProperty]
    public partial string AudioModelPath { get; set; } = "";

    [ObservableProperty]
    public partial string AudioSystemPrompt { get; set; } = "";

    [ObservableProperty]
    public partial bool AudioUseGpu { get; set; }

    [ObservableProperty]
    public partial bool IsModelDownloading { get; set; }

    [ObservableProperty]
    public partial string DownloadModelButtonText { get; set; } = "Download Model";

    [ObservableProperty]
    public partial string? UpdateStatusText { get; set; }

    [ObservableProperty]
    public partial bool IsUpdateAvailable { get; set; }

    [ObservableProperty]
    public partial bool IsUpdating { get; set; }

    [ObservableProperty]
    public partial string UpdateButtonText { get; set; }

    public string VersionText { get; } = $"v{UpdateService.CurrentVersion}";

    [ObservableProperty]
    public partial bool IsRegionMode { get; set; }

    [ObservableProperty]
    public partial bool IsWindowMode { get; set; }

    [ObservableProperty]
    public partial string RegionDisplayText { get; set; } = "";

    [ObservableProperty]
    public partial string SelectedWindowTitle { get; set; } = "";

    public void Receive(RegionSelectedMessage message)
    {
        var capture = settingsService.Settings.Capture;
        capture.RegionX = message.X;
        capture.RegionY = message.Y;
        capture.RegionWidth = message.Width;
        capture.RegionHeight = message.Height;
        RegionDisplayText = $"{message.Width}\u00D7{message.Height} at ({message.X}, {message.Y})";
        settingsService.SaveDebounced();
    }

    public void Receive(WindowSelectedMessage message)
    {
        settingsService.Settings.Capture.WindowHandle = message.Handle;
        settingsService.Settings.Capture.WindowTitle = message.Title;
        SelectedWindowTitle = message.Title;
        settingsService.SaveDebounced();
    }

    partial void OnSelectedProviderIndexChanged(int value)
    {
        WeakReferenceMessenger.Default.Send(new SettingsProviderChangedMessage(value));
    }

    partial void OnOpacityChanged(double value)
    {
        settingsService.Settings.WindowOpacity = value;
        OpacityValueText = $"{(int)(value * 100)}%";
        WeakReferenceMessenger.Default.Send(new OpacityChangedMessage(value));
        settingsService.SaveDebounced();
    }

    partial void OnSelectedCaptureModeIndexChanged(int value)
    {
        settingsService.Settings.Capture.Mode = (CaptureMode)value;
        IsRegionMode = value == (int)CaptureMode.Region;
        IsWindowMode = value == (int)CaptureMode.Window;
        settingsService.SaveDebounced();
    }

    partial void OnHotkeyChanged(string value)
    {
        settingsService.Settings.Capture.Hotkey = value;
        WeakReferenceMessenger.Default.Send(new HotkeyChangedMessage("capture", value));
        settingsService.SaveDebounced();
    }

    partial void OnMultiCaptureHotkeyChanged(string value)
    {
        settingsService.Settings.Capture.MultiCaptureHotkey = value;
        WeakReferenceMessenger.Default.Send(new HotkeyChangedMessage("multicapture", value));
        settingsService.SaveDebounced();
    }

    partial void OnOpacityHotkeyChanged(string value)
    {
        settingsService.Settings.OpacityHotkey = value;
        WeakReferenceMessenger.Default.Send(new HotkeyChangedMessage("opacity", value));
        settingsService.SaveDebounced();
    }

    partial void OnNoFocusHotkeyChanged(string value)
    {
        settingsService.Settings.NoFocusHotkey = value;
        WeakReferenceMessenger.Default.Send(new HotkeyChangedMessage("nofocus", value));
        settingsService.SaveDebounced();
    }

    partial void OnSystemPromptChanged(string value)
    {
        settingsService.Settings.Capture.SystemPrompt = value;
        settingsService.SaveDebounced();
    }

    partial void OnAudioHotkeyChanged(string value)
    {
        settingsService.Settings.Audio.Hotkey = value;
        WeakReferenceMessenger.Default.Send(new HotkeyChangedMessage("audio", value));
        settingsService.SaveDebounced();
    }

    partial void OnAudioModelPathChanged(string value)
    {
        settingsService.Settings.Audio.ModelPath = value;
        settingsService.SaveDebounced();
    }

    partial void OnAudioSystemPromptChanged(string value)
    {
        settingsService.Settings.Audio.SystemPrompt = value;
        settingsService.SaveDebounced();
    }

    partial void OnAudioUseGpuChanged(bool value)
    {
        settingsService.Settings.Audio.UseGpu = value;
        settingsService.SaveDebounced();
    }

    [RelayCommand]
    private void SelectRegion()
    {
        WeakReferenceMessenger.Default.Send(new RequestRegionSelectionMessage());
    }

    [RelayCommand]
    private void SelectWindow()
    {
        WeakReferenceMessenger.Default.Send(new RequestWindowSelectionMessage());
    }

    public void Load()
    {
        var settings = settingsService.Settings;

        var providers = providerRegistry.GetAllProviders();
        ProviderNames = [.. providers.Select(p => p.Name)];

        var index = providers.ToList().FindIndex(p => p.Id == settings.ActiveProviderId);
        SelectedProviderIndex = index >= 0 ? index : 0;

        Opacity = settings.WindowOpacity;
        OpacityValueText = $"{(int)(settings.WindowOpacity * 100)}%";

        SelectedCaptureModeIndex = (int)settings.Capture.Mode;
        IsRegionMode = settings.Capture.Mode == CaptureMode.Region;
        IsWindowMode = settings.Capture.Mode == CaptureMode.Window;

        Hotkey = settings.Capture.Hotkey;
        MultiCaptureHotkey = settings.Capture.MultiCaptureHotkey;
        OpacityHotkey = settings.OpacityHotkey;
        NoFocusHotkey = settings.NoFocusHotkey;
        SystemPrompt = settings.Capture.SystemPrompt;

        if (settings.Capture is { RegionWidth: > 0, RegionHeight: > 0 })
        {
            RegionDisplayText =
                $"{settings.Capture.RegionWidth}\u00D7{settings.Capture.RegionHeight} at ({settings.Capture.RegionX}, {settings.Capture.RegionY})";
        }
        else
        {
            RegionDisplayText = "";
        }

        SelectedWindowTitle = settings.Capture.WindowTitle;

        AudioHotkey = settings.Audio.Hotkey;
        AudioModelPath = settings.Audio.ModelPath;
        AudioSystemPrompt = settings.Audio.SystemPrompt;
        AudioUseGpu = settings.Audio.UseGpu;
        DownloadModelButtonText = ModelDownloadService.ModelExists(settings.Audio.ModelPath)
            ? "Model ready"
            : "Download Model";

        UpdateButtonText = IsUpdateAvailable ? "Update & Restart" : "Check for Updates";

        WeakReferenceMessenger.Default.Register<RegionSelectedMessage>(this);
        WeakReferenceMessenger.Default.Register<WindowSelectedMessage>(this);
    }

    public void Unload()
    {
        WeakReferenceMessenger.Default.Unregister<RegionSelectedMessage>(this);
        WeakReferenceMessenger.Default.Unregister<WindowSelectedMessage>(this);
    }

    [RelayCommand]
    private void ResetPrompt()
    {
        var provider = providerRegistry.GetActiveProvider();
        SystemPrompt = provider.DefaultSystemPrompt;
    }

    [RelayCommand]
    private void ResetAudioPrompt()
    {
        AudioSystemPrompt = new AudioSettings().SystemPrompt;
    }

    [RelayCommand]
    private async Task CheckForUpdate()
    {
        if (IsUpdating)
        {
            return;
        }

        if (IsUpdateAvailable && pendingRelease is not null)
        {
            await ApplyUpdate();
            return;
        }

        UpdateButtonText = "Checking...";
        var release = await updateService.CheckForUpdateAsync();

        if (release is not null)
        {
            pendingRelease = release;
            IsUpdateAvailable = true;
            var version = release.TagName;
            UpdateStatusText = $"New version available: {version}";
            UpdateButtonText = "Update & Restart";
            WeakReferenceMessenger.Default.Send(new UpdateAvailableMessage(true));
        }
        else
        {
            UpdateStatusText = "You're on the latest version";
            UpdateButtonText = "Check for Updates";
        }
    }

    private async Task ApplyUpdate()
    {
        if (pendingRelease is null)
        {
            return;
        }

        IsUpdating = true;
        UpdateButtonText = "Downloading...";

        updateService.DownloadProgress += OnUpdateDownloadProgress;
        var success = await updateService.DownloadAndApplyAsync(pendingRelease);
        updateService.DownloadProgress -= OnUpdateDownloadProgress;

        if (success)
        {
            UpdateButtonText = "Restarting...";
            UpdateService.LaunchUpdateAndExit();
        }
        else
        {
            IsUpdating = false;
            UpdateButtonText = "Update & Restart";
            UpdateStatusText = "Update failed. Try again.";
        }
    }

    private void OnUpdateDownloadProgress(long downloaded, long total)
    {
        if (total > 0)
        {
            var pct = (int)(downloaded * 100 / total);
            Dispatcher.UIThread.Post(() => UpdateButtonText = $"Downloading... {pct}%");
        }
    }

    [RelayCommand]
    private async Task DownloadModel()
    {
        var modelPath = settingsService.Settings.Audio.ModelPath;
        if (ModelDownloadService.ModelExists(modelPath))
        {
            DownloadModelButtonText = "Model already exists";
            return;
        }

        IsModelDownloading = true;
        DownloadModelButtonText = "Downloading...";
        WeakReferenceMessenger.Default.Send(new ModelDownloadRequestedMessage(modelPath));
    }

    public void OnDownloadCompleted(bool success)
    {
        IsModelDownloading = false;
        DownloadModelButtonText = success ? "Downloaded" : "Download Model";
    }
}
