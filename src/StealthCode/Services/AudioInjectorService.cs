using StealthCode.Audio.Models;
using StealthCode.Audio.Services;
using StealthCode.Terminal;

namespace StealthCode.Services;

public sealed record AudioStateChangedEventArgs(bool IsListening, string Status, string Transcript);

/// <summary>Streams loopback audio through Whisper and injects finished utterances into the terminal.</summary>
public sealed class AudioInjectorService
{
    private readonly SettingsService settingsService;
    private readonly LiveTranscriptionService live;
    private readonly PtyService pty;
    private string transcript = "";
    private string status = "";
    private bool starting;

    public AudioInjectorService(SettingsService settingsService, LiveTranscriptionService live, PtyService pty)
    {
        this.settingsService = settingsService;
        this.live = live;
        this.pty = pty;

        live.StateChanged += OnStateChanged;
        live.PartialTranscript += OnPartialTranscript;
        live.UtteranceCompleted += OnUtteranceCompleted;
        live.Failed += OnFailed;
    }

    public string? LastError => live.LastError;

    /// <summary>Raised when listening state, status text, or the transcript preview changes.</summary>
    public event Action<AudioStateChangedEventArgs>? AudioStateChanged;

    /// <summary>Toggles listening. Model loading and transcription run in the background.</summary>
    public bool Toggle()
    {
        if (starting)
        {
            return false;
        }

        if (live.IsListening)
        {
            _ = live.StopAsync();
            return false;
        }

        starting = true;
        transcript = "";
        Raise(true, "Loading model...");
        var audio = settingsService.Settings.Audio;

        Task.Run(async () =>
        {
            var ok = await live.StartAsync(audio);
            starting = false;

            if (live.GpuFellBack)
            {
                audio.GpuBackend = GpuBackend.None;
                settingsService.Save();
            }

            if (!ok)
            {
                transcript = "";
                Raise(false, live.LastError ?? "Audio capture failed");
            }
        });

        return true;
    }

    private void OnStateChanged(LiveTranscriptionState state)
    {
        var status = state switch
        {
            LiveTranscriptionState.Listening => "Listening",
            LiveTranscriptionState.Hearing => "Hearing...",
            LiveTranscriptionState.Transcribing => "Transcribing...",
            LiveTranscriptionState.LoadingModel => "Loading model...",
            LiveTranscriptionState.Stopping => "Finishing...",
            _ => live.LastError ?? ""
        };

        Raise(live.IsListening, status);
    }

    private void OnPartialTranscript(string text)
    {
        transcript = text;
        Raise(live.IsListening, status);
    }

    private void OnUtteranceCompleted(string text)
    {
        var audio = settingsService.Settings.Audio;
        _ = PromptInjector.SendAsync(pty, $"{audio.SystemPrompt.Trim()}\n\n{text}");
        Raise(live.IsListening, "Sent");
    }

    private void OnFailed(string message)
    {
        transcript = "";
        Raise(false, message);

        if (live.IsListening)
        {
            _ = live.StopAsync();
        }
    }

    private void Raise(bool isListening, string newStatus)
    {
        status = newStatus;
        AudioStateChanged?.Invoke(new AudioStateChangedEventArgs(isListening, newStatus, transcript));
    }
}
