using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using StealthCode.Audio.Models;
using StealthCode.Audio.Services;
using StealthCode.Messages;
using StealthCode.Services;
using StealthCode.Utilities;

namespace StealthCode.ViewModels.Settings;

/// <summary>Whisper model, end-of-utterance pause, and prompt. The GPU pack lives in <see cref="Gpu"/>.</summary>
public sealed partial class AudioSettingsViewModel : SettingsSectionViewModel
{
    private readonly WhisperModelInstaller modelInstaller;

    public AudioSettingsViewModel(
        SettingsService settingsService,
        WhisperModelInstaller modelInstaller,
        GpuPackSettingsViewModel gpu) : base(settingsService)
    {
        this.modelInstaller = modelInstaller;
        Gpu = gpu;

        // Subscribed for the lifetime of the view model: a download outlives the open settings panel.
        modelInstaller.Progress += OnDownloadProgress;
        modelInstaller.Completed += OnDownloadCompleted;
    }

    public GpuPackSettingsViewModel Gpu { get; }

    [ObservableProperty]
    public partial WhisperModel? SelectedModel { get; set; }

    [ObservableProperty]
    public partial string Language { get; set; } = "en";

    [ObservableProperty]
    public partial double EndOfUtteranceSeconds { get; set; } = 1.8;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SystemPromptPreview))]
    public partial string SystemPrompt { get; set; } = "";

    [ObservableProperty]
    public partial bool IsPromptExpanded { get; set; }

    /// <summary>Language, runtime and pack management: configured once, if ever.</summary>
    [ObservableProperty]
    public partial bool IsAdvancedExpanded { get; set; }

    [ObservableProperty]
    public partial bool IsModelDownloading { get; set; }

    /// <summary>Feeds the download progress bar.</summary>
    [ObservableProperty]
    public partial double DownloadProgress { get; set; }

    [ObservableProperty]
    public partial string DownloadModelButtonText { get; set; } = "Download Model";

    public string SystemPromptPreview => PromptPreview.Of(SystemPrompt);

    protected override void LoadCore()
    {
        var audio = SettingsService.Settings.Audio;

        SelectedModel = WhisperModelCatalog.Resolve(audio.ModelPath);
        Language = audio.Language;
        EndOfUtteranceSeconds = audio.EndOfUtteranceMs / 1000.0;
        SystemPrompt = audio.SystemPrompt;
        DownloadModelButtonText = ModelLabel(audio.ModelPath);

        Gpu.Load();
    }

    [RelayCommand]
    private void ResetPrompt() => SystemPrompt = new AudioSettings().SystemPrompt;

    [RelayCommand]
    private void TogglePrompt() => IsPromptExpanded = !IsPromptExpanded;

    [RelayCommand]
    private void ToggleAdvanced() => IsAdvancedExpanded = !IsAdvancedExpanded;

    [RelayCommand]
    private void CancelModelDownload() => modelInstaller.Cancel();

    [RelayCommand]
    private async Task DownloadModel()
    {
        IsModelDownloading = true;
        DownloadModelButtonText = "Downloading...";
        await modelInstaller.InstallAsync();
    }

    private void OnDownloadProgress(long downloaded, long total)
    {
        if (total > 0)
        {
            DownloadProgress = downloaded * 100.0 / total;
        }
    }

    private void OnDownloadCompleted(bool success)
    {
        IsModelDownloading = false;
        DownloadProgress = 0;
        DownloadModelButtonText = success ? "Model ready" : "Download failed - retry";
    }

    private static string ModelLabel(string modelPath) =>
        ModelDownloadService.ModelExists(modelPath) ? "Model ready" : "Download Model";

    partial void OnSelectedModelChanged(WhisperModel? value)
    {
        if (IsLoading || value is null)
        {
            return;
        }

        SettingsService.Settings.Audio.ModelPath = value.Path;
        Save();
        DownloadModelButtonText = ModelDownloadService.ModelExists(value.Path)
            ? "Model ready"
            : $"Download ({value.SizeText})";
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
}
