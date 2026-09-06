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
    private bool hotkeyRegistered;

    [ObservableProperty]
    public partial bool IsListening { get; set; }

    [ObservableProperty]
    public partial string HotkeyText { get; set; } = "\u23FA Ctrl+Shift+A";

    [ObservableProperty]
    public partial string TranscriptText { get; set; } = "";

    [ObservableProperty]
    public partial bool IsTranscriptPanelVisible { get; set; }

    [ObservableProperty]
    public partial bool IsModelAvailable { get; set; }

    [ObservableProperty]
    public partial bool IsModelDownloading { get; set; }

    [ObservableProperty]
    public partial string StatusText { get; set; } = "";

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
            IsModelAvailable = true;
            StatusText = "";
            RegisterHotkey();
        }
        else
        {
            StatusText = "Download failed";
        }

        WeakReferenceMessenger.Default.Send(new ModelDownloadCompletedMessage(success));
    }

    public void Receive(AudioModelChangedMessage message)
    {
        IsModelAvailable = ModelDownloadService.ModelExists(message.ModelPath);

        if (IsModelAvailable)
        {
            if (!hotkeyRegistered)
            {
                RegisterHotkey();
            }

            StatusText = "";
        }
        else
        {
            hotkeyService.Unregister("audio");
            hotkeyRegistered = false;
            StatusText = "Whisper model not found, please download by clicking the button in settings";
        }
    }

    public void Initialize(IntPtr windowHandle)
    {
        hwnd = windowHandle;

        IsModelAvailable = ModelDownloadService.ModelExists(settingsService.Settings.Audio.ModelPath);
        if (!IsModelAvailable)
        {
            StatusText = "Whisper model not found, please download by clicking the button in settings";
        }

        if (IsModelAvailable)
        {
            RegisterHotkey();
        }

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
            IsTranscriptPanelVisible = e.IsListening || e.Transcript.Length > 0;
            WeakReferenceMessenger.Default.Send(new AudioRecordingChangedMessage(e.IsListening));
        });
    }

    public void LoadFromSettings()
    {
        HotkeyText = $"\u23FA {settingsService.Settings.Audio.Hotkey}";
    }

    private void RegisterHotkey()
    {
        if (hwnd != IntPtr.Zero && hotkeyService.Register("audio", settingsService.Settings.Audio.Hotkey, hwnd, Toggle))
        {
            hotkeyRegistered = true;
        }
    }

    private void OnDownloadProgress(long downloaded, long total)
    {
        StatusText = $"Downloading model... {DownloadProgressText.Format(downloaded, total)}";
    }
}
