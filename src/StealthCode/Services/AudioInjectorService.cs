using System.Text;
using StealthCode.Audio.Models;
using StealthCode.Audio.Services;
using StealthCode.Terminal;

namespace StealthCode.Services;

public sealed record AudioStateChangedEventArgs(bool IsRecording, string Status);

/// <summary>Captures audio, transcribes it, and injects the result into the terminal.</summary>
public sealed class AudioInjectorService(
    SettingsService settingsService,
    AudioCaptureService audioCaptureService,
    TranscriptionService transcriptionService,
    PtyService pty)
{
    private static readonly byte[] Enter = "\r"u8.ToArray();

    public string? LastError => audioCaptureService.LastError;

    /// <summary>Raised when recording or transcription status changes.</summary>
    public event Action<AudioStateChangedEventArgs>? AudioStateChanged;

    /// <summary>Toggles recording. Processing after stop runs in the background.</summary>
    public bool Toggle()
    {
        if (!audioCaptureService.IsRecording)
        {
            if (audioCaptureService.StartCapture())
            {
                AudioStateChanged?.Invoke(new AudioStateChangedEventArgs(true, ""));
                return true;
            }

            return false;
        }

        // Stop and process in the background.
        AudioStateChanged?.Invoke(new AudioStateChangedEventArgs(false, "Saving audio..."));
        var wavPath = audioCaptureService.StopCapture();

        if (wavPath is null)
        {
            AudioStateChanged?.Invoke(new AudioStateChangedEventArgs(false, ""));
            return false;
        }

        var audio = settingsService.Settings.Audio;

        Task.Run(async () =>
        {
            AudioStateChanged?.Invoke(new AudioStateChangedEventArgs(false, "Transcribing audio..."));
            var result = await transcriptionService.TranscribeAsync(wavPath, audio);

            // A GPU runtime that failed to load fell back to the CPU; make that stick.
            if (result.GpuFellBack)
            {
                audio.GpuBackend = GpuBackend.None;
                settingsService.Save();
            }

            // Only write successful transcripts to the terminal.
            if (!result.Ok)
            {
                AudioStateChanged?.Invoke(new AudioStateChangedEventArgs(false, result.Error ?? ""));
                return;
            }

            var transcriptPath = Path.ChangeExtension(wavPath, ".txt");
            await File.WriteAllTextAsync(transcriptPath, result.Text);

            var prompt = $"{audio.SystemPrompt.Trim()} See the transcription file: {transcriptPath.Replace('\\', '/')}";
            pty.Write(Encoding.UTF8.GetBytes(prompt));
            await Task.Delay(500);
            pty.Write(Enter);
            AudioStateChanged?.Invoke(new AudioStateChangedEventArgs(false, ""));
        });

        return false;
    }
}
