using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Messaging;
using StealthCode.Audio.Services;
using StealthCode.Messages;
using StealthCode.Services;
using StealthCode.Utilities;

namespace StealthCode.ViewModels;

// ReSharper disable once PartialTypeWithSinglePart
public sealed partial class AudioViewModel(
    SettingsService settingsService,
    HotkeyService hotkeyService,
    AudioInjectorService audioInjectorService,
    ModelDownloadService modelDownloadService) : ViewModelBase,
    IRecipient<ModelDownloadRequestedMessage>, IRecipient<AudioModelChangedMessage>
{
    private IntPtr hwnd;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsTranscriptPanelVisible))]
    public partial bool IsListening { get; set; }

    [ObservableProperty]
    public partial string HotkeyText { get; set; } = "\u23FA Ctrl+Shift+A";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsTranscriptPanelVisible))]
    public partial string TranscriptText { get; set; } = "";

    [ObservableProperty]
    public partial bool IsModelAvailable { get; set; }

    [ObservableProperty]
    public partial bool IsModelDownloading { get; set; }

    [ObservableProperty]
    public partial string StatusText { get; set; } = "";

    /// <summary>The panel shows while listening and stays up afterwards to hold the last transcript.</summary>
    public bool IsTranscriptPanelVisible => IsListening || TranscriptText.Length > 0;

    public async void Receive(ModelDownloadRequestedMessage message)
    {
        if (IsModelDownloading)
        {
            return;
        }

        var modelPath = settingsService.Settings.Audio.ModelPath;
        var modelFileName = Path.GetFileName(modelPath);

        IsModelDownloading = true;
        StatusText = "Downloading model...";

        modelDownloadService.DownloadProgress += OnDownloadProgress;
        var success = await modelDownloadService.DownloadAsync(modelFileName, modelPath);
        modelDownloadService.DownloadProgress -= OnDownloadProgress;

        IsModelDownloading = false;

        if (success)
        {
            ApplyModelAvailability(modelPath);
        }
        else
        {
            StatusText = "Download failed";
        }

        WeakReferenceMessenger.Default.Send(new ModelDownloadCompletedMessage(success));
    }

    public void Receive(AudioModelChangedMessage message) => ApplyModelAvailability(message.ModelPath);

    public void Initialize(IntPtr windowHandle)
    {
        hwnd = windowHandle;

        ApplyModelAvailability(settingsService.Settings.Audio.ModelPath);

        audioInjectorService.AudioStateChanged += OnAudioStateChanged;
        WeakReferenceMessenger.Default.Register<ModelDownloadRequestedMessage>(this);
        WeakReferenceMessenger.Default.Register<AudioModelChangedMessage>(this);
    }

    public void OnHotkeyChanged(string hotkey)
    {
        HotkeyText = $"\u23FA {hotkey}";
        if (IsModelAvailable)
        {
            RegisterHotkey();
        }
    }

    public void Toggle()
    {
        if (IsListening)
        {
            audioInjectorService.Toggle();
            return;
        }

        if (!IsModelAvailable || IsModelDownloading)
        {
            StatusText = "Whisper model not ready";
            return;
        }

        var started = audioInjectorService.Toggle();

        if (!started)
        {
            StatusText = audioInjectorService.LastError ?? "Audio capture failed";
        }
    }

    public void Cleanup()
    {
        audioInjectorService.AudioStateChanged -= OnAudioStateChanged;
        WeakReferenceMessenger.Default.Unregister<ModelDownloadRequestedMessage>(this);
        WeakReferenceMessenger.Default.Unregister<AudioModelChangedMessage>(this);
    }

    private void OnAudioStateChanged(AudioStateChangedEventArgs e)
    {
        Dispatcher.UIThread.Post(() =>
        {
            IsListening = e.IsListening;
            StatusText = e.Status;
            TranscriptText = e.Transcript;
            WeakReferenceMessenger.Default.Send(new AudioRecordingChangedMessage(e.IsListening));
        });
    }

    public void LoadFromSettings()
    {
        HotkeyText = $"\u23FA {settingsService.Settings.Audio.Hotkey}";
    }

    /// <summary>Points the hotkey at the model: registered once it is on disk, dropped with a hint when it is not.</summary>
    private void ApplyModelAvailability(string modelPath)
    {
        IsModelAvailable = ModelDownloadService.ModelExists(modelPath);

        if (IsModelAvailable)
        {
            StatusText = "";
            RegisterHotkey();
        }
        else
        {
            hotkeyService.Unregister("audio");
            StatusText = "Whisper model not found, please download by clicking the button in settings";
        }
    }

    private void RegisterHotkey()
    {
        // Register replaces any existing "audio" hotkey, so it is safe to call again.
        hotkeyService.Register("audio", settingsService.Settings.Audio.Hotkey, hwnd, Toggle);
    }

    private void OnDownloadProgress(long downloaded, long total)
    {
        StatusText = $"Downloading model... {DownloadProgressText.Format(downloaded, total)}";
    }
}
