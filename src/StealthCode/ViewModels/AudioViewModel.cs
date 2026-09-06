using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using StealthCode.Audio.Services;
using StealthCode.Messages;
using StealthCode.Models;
using StealthCode.Services;
using StealthCode.Utilities;

namespace StealthCode.ViewModels;

// ReSharper disable once PartialTypeWithSinglePart
public sealed partial class AudioViewModel(
    SettingsService settingsService,
    HotkeyService hotkeyService,
    AudioInjectorService audioInjectorService,
    WhisperModelInstaller modelInstaller) : ViewModelBase,
    IRecipient<AudioModelChangedMessage>
{
    private IntPtr hwnd;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsTranscriptPanelVisible))]
    public partial bool IsListening { get; set; }

    [ObservableProperty]
    public partial string Hotkey { get; set; } = "Ctrl+Shift+A";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsTranscriptPanelVisible))]
    public partial string TranscriptText { get; set; } = "";

    [ObservableProperty]
    public partial bool IsModelAvailable { get; set; }

    [ObservableProperty]
    public partial string StatusText { get; set; } = "";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsStatusWarning))]
    [NotifyPropertyChangedFor(nameof(IsStatusError))]
    public partial StatusLevel StatusLevel { get; set; } = StatusLevel.Info;

    // Severity reaches the view as style classes.
    public bool IsStatusWarning => StatusLevel == StatusLevel.Warning;

    public bool IsStatusError => StatusLevel == StatusLevel.Error;

    /// <summary>The panel shows while listening and stays up afterwards to hold the last transcript.</summary>
    public bool IsTranscriptPanelVisible => IsListening || TranscriptText.Length > 0;

    public void Receive(AudioModelChangedMessage message) => ApplyModelAvailability(message.ModelPath);

    public void Initialize(IntPtr windowHandle)
    {
        hwnd = windowHandle;

        ApplyModelAvailability(settingsService.Settings.Audio.ModelPath);

        audioInjectorService.AudioStateChanged += OnAudioStateChanged;
        modelInstaller.Progress += OnDownloadProgress;
        modelInstaller.Completed += OnDownloadCompleted;
        WeakReferenceMessenger.Default.Register<AudioModelChangedMessage>(this);
    }

    public void Cleanup()
    {
        audioInjectorService.AudioStateChanged -= OnAudioStateChanged;
        modelInstaller.Progress -= OnDownloadProgress;
        modelInstaller.Completed -= OnDownloadCompleted;
        WeakReferenceMessenger.Default.Unregister<AudioModelChangedMessage>(this);
    }

    /// <summary>Not OnHotkeyChanged: that name collides with the generated hook for Hotkey.</summary>
    public void ApplyHotkey(string hotkey)
    {
        Hotkey = hotkey;

        if (IsModelAvailable)
        {
            RegisterHotkey();
        }
    }

    public void LoadFromSettings() => Hotkey = settingsService.Settings.Audio.Hotkey;

    [RelayCommand]
    public void Toggle()
    {
        if (IsListening)
        {
            audioInjectorService.Toggle();
            return;
        }

        if (!IsModelAvailable || modelInstaller.IsDownloading)
        {
            SetStatus("Whisper model not ready", StatusLevel.Warning);
            return;
        }

        if (!audioInjectorService.Toggle())
        {
            SetStatus(audioInjectorService.LastError ?? "Audio capture failed", StatusLevel.Error);
        }
    }

    private void OnAudioStateChanged(AudioStateChangedEventArgs e)
    {
        Dispatcher.UIThread.Post(() =>
        {
            IsListening = e.IsListening;
            SetStatus(e.Status, StatusLevel.Info);
            TranscriptText = e.Transcript;
            WeakReferenceMessenger.Default.Send(new AudioRecordingChangedMessage(e.IsListening));
        });
    }

    private void OnDownloadProgress(long downloaded, long total) =>
        SetStatus($"Downloading model... {DownloadProgressText.Format(downloaded, total)}", StatusLevel.Info);

    private void OnDownloadCompleted(bool success)
    {
        if (success)
        {
            ApplyModelAvailability(settingsService.Settings.Audio.ModelPath);
        }
        else
        {
            SetStatus("Model download failed", StatusLevel.Error);
        }
    }

    private void SetStatus(string text, StatusLevel level)
    {
        StatusText = text;
        StatusLevel = level;
    }

    /// <summary>Points the hotkey at the model: registered once it is on disk, dropped with a hint when it is not.</summary>
    private void ApplyModelAvailability(string modelPath)
    {
        IsModelAvailable = ModelDownloadService.ModelExists(modelPath);

        if (IsModelAvailable)
        {
            SetStatus("", StatusLevel.Info);
            RegisterHotkey();
        }
        else
        {
            hotkeyService.Unregister("audio");
            SetStatus("No Whisper model", StatusLevel.Warning);
        }
    }

    private void RegisterHotkey()
    {
        // Register replaces any existing "audio" hotkey, so it is safe to call again.
        hotkeyService.Register("audio", settingsService.Settings.Audio.Hotkey, hwnd, Toggle);
    }
}
