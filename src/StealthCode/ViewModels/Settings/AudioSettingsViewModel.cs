using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using StealthCode.Audio.Models;
using StealthCode.Audio.Services;
using StealthCode.Messages;
using StealthCode.Services;
using StealthCode.Utilities;

namespace StealthCode.ViewModels.Settings;

/// <summary>Audio hotkey, Whisper model, GPU pack, and prompt.</summary>
public sealed partial class AudioSettingsViewModel : SettingsSectionViewModel,
    IRecipient<ModelDownloadCompletedMessage>
{
    private readonly GpuPackService gpuPackService;

    public AudioSettingsViewModel(SettingsService settingsService, GpuPackService gpuPackService)
        : base(settingsService)
    {
        this.gpuPackService = gpuPackService;

        // Registered for the lifetime of the view model: a download outlives the open settings panel.
        WeakReferenceMessenger.Default.Register<ModelDownloadCompletedMessage>(this);
    }

    [ObservableProperty]
    public partial string Hotkey { get; set; } = "Ctrl+Shift+A";

    [ObservableProperty]
    public partial WhisperModel? SelectedModel { get; set; }

    [ObservableProperty]
    public partial string Language { get; set; } = "en";

    [ObservableProperty]
    public partial double EndOfUtteranceSeconds { get; set; } = 1.8;

    [ObservableProperty]
    public partial string SystemPrompt { get; set; } = "";

    [ObservableProperty]
    public partial bool IsModelDownloading { get; set; }

    [ObservableProperty]
    public partial string DownloadModelButtonText { get; set; } = "Download Model";

    /// <summary>Runtime Whisper should use, whether or not its pack is installed.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasGpuPack))]
    public partial GpuBackend SelectedGpuBackend { get; set; }

    [ObservableProperty]
    public partial string? GpuPackStatusText { get; set; }

    [ObservableProperty]
    public partial string GpuPackButtonText { get; set; } = "Install";

    [ObservableProperty]
    public partial bool IsGpuPackBusy { get; set; }

    /// <summary>Whether the selected backend needs a pack, and so has buttons to show.</summary>
    public bool HasGpuPack => SelectedPack is not null;

    private GpuPack? SelectedPack => GpuPackCatalog.Resolve(SelectedGpuBackend);

    public void Receive(ModelDownloadCompletedMessage message)
    {
        IsModelDownloading = false;
        DownloadModelButtonText = message.Success ? "Downloaded" : "Download Model";

        if (message.Success)
        {
            WeakReferenceMessenger.Default.Send(new AudioModelChangedMessage(SettingsService.Settings.Audio.ModelPath));
        }
    }

    protected override void LoadCore()
    {
        var audio = SettingsService.Settings.Audio;

        Hotkey = audio.Hotkey;
        SelectedModel = WhisperModelCatalog.Resolve(audio.ModelPath);
        Language = audio.Language;
        EndOfUtteranceSeconds = audio.EndOfUtteranceMs / 1000.0;
        SystemPrompt = audio.SystemPrompt;
        SelectedGpuBackend = audio.GpuBackend;
        DownloadModelButtonText = ModelDownloadService.ModelExists(audio.ModelPath)
            ? "Model ready"
            : "Download Model";

        GpuPackStatusText = RefreshGpuPackButton();
    }

    [RelayCommand]
    private void ResetPrompt() => SystemPrompt = new AudioSettings().SystemPrompt;

    [RelayCommand]
    private void DownloadModel()
    {
        var modelPath = SettingsService.Settings.Audio.ModelPath;

        if (ModelDownloadService.ModelExists(modelPath))
        {
            DownloadModelButtonText = "Model already exists";
            return;
        }

        IsModelDownloading = true;
        DownloadModelButtonText = "Downloading...";
        WeakReferenceMessenger.Default.Send(new ModelDownloadRequestedMessage(modelPath));
    }

    /// <summary>Installs or removes the pack for the selected backend.</summary>
    [RelayCommand]
    private async Task ToggleGpuPack()
    {
        if (SelectedPack is not { } pack)
        {
            return;
        }

        if (pack.IsInstalled())
        {
            var removed = gpuPackService.Remove(pack);
            var status = RefreshGpuPackButton();
            GpuPackStatusText = removed ? status : "In use right now. It will go on the next launch.";
            return;
        }

        IsGpuPackBusy = true;
        GpuPackStatusText = "Starting download...";
        gpuPackService.Progress += OnGpuPackProgress;

        try
        {
            var message = await gpuPackService.InstallAsync(pack);
            RefreshGpuPackButton();

            // Show the actual result, which the status line above only guessed at.
            GpuPackStatusText = message;
        }
        finally
        {
            gpuPackService.Progress -= OnGpuPackProgress;
            IsGpuPackBusy = false;
        }
    }

    [RelayCommand]
    private void CancelGpuPack() => gpuPackService.Cancel();

    /// <summary>Points the install button at the selected pack, and returns the matching status line.</summary>
    private string? RefreshGpuPackButton()
    {
        if (SelectedPack is not { } pack)
        {
            return null;
        }

        var installed = pack.IsInstalled();
        GpuPackButtonText = installed ? "Remove" : $"Download {pack.SizeText}";

        return installed
            ? $"{pack.DisplayName} is installed."
            : $"{pack.DisplayName} needs a {pack.SizeText} download.";
    }

    private void OnGpuPackProgress(long downloaded, long total)
    {
        // Format off the UI thread so the post carries only the finished string.
        var text = $"Downloading... {DownloadProgressText.Format(downloaded, total)}";
        Dispatcher.UIThread.Post(() => GpuPackStatusText = text);
    }

    partial void OnHotkeyChanged(string value)
    {
        SettingsService.Settings.Audio.Hotkey = value;
        SaveHotkey("audio", value);
    }

    partial void OnSelectedModelChanged(WhisperModel? value)
    {
        if (IsLoading || value is null)
        {
            return;
        }

        SettingsService.Settings.Audio.ModelPath = value.Path;
        Save();
        DownloadModelButtonText = value.IsDownloaded() ? "Model ready" : $"Download ({value.SizeText})";
        WeakReferenceMessenger.Default.Send(new AudioModelChangedMessage(value.Path));
    }

    partial void OnLanguageChanged(string value)
    {
        if (IsLoading)
        {
            return;
        }

        SettingsService.Settings.Audio.Language = value;
        Save();
    }

    partial void OnEndOfUtteranceSecondsChanged(double value)
    {
        if (IsLoading)
        {
            return;
        }

        SettingsService.Settings.Audio.EndOfUtteranceMs = (int)Math.Round(value * 1000);
        Save();
    }

    partial void OnSystemPromptChanged(string value)
    {
        SettingsService.Settings.Audio.SystemPrompt = value;
        Save();
    }

    partial void OnSelectedGpuBackendChanged(GpuBackend value)
    {
        if (IsLoading)
        {
            return;
        }

        SettingsService.Settings.Audio.GpuBackend = value;
        Save();
        GpuPackStatusText = RefreshGpuPackButton();
    }
}
