using System.Text;
using StealthCode.Audio.Services;
using StealthCode.Terminal;

namespace StealthCode.Services;

public sealed record AudioStateChangedEventArgs(bool IsRecording, string Status);

/// <summary>
///     Orchestrates audio capture, transcription, and injection into the terminal.
///     Toggle pattern: the first call starts recording, the second call stops → transcribes → injects.
/// </summary>
public sealed class AudioInjectorService(
    SettingsService settingsService,
    AudioCaptureService audioCaptureService,
    TranscriptionService transcriptionService,
    PtyService pty)
{
    private static readonly byte[] Enter = "\r"u8.ToArray();

    public string? LastError => audioCaptureService.LastError;

    /// <summary>
    ///    Raised when audio recording state changes such as start, stop, or status updates (e.g. "Transcribing audio...").
    /// </summary>
    public event Action<AudioStateChangedEventArgs>? AudioStateChanged;

    /// <summary>
    ///     Toggles audio recording. Returns true if recording started, false if stopped.
    ///     On stop, transcription and injection happen on a background thread.
    /// </summary>
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

        // Stop recording and process on background thread
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
            var useGpuBefore = audio.UseGpu;
            var transcript = await transcriptionService.TranscribeAsync(wavPath, audio);

            // Transcribing clears UseGpu when the graphics card closed the app last run and was skipped.
            if (audio.UseGpu != useGpuBefore)
            {
                settingsService.Save();
            }

            if (string.IsNullOrWhiteSpace(transcript))
            {
                AudioStateChanged?.Invoke(new AudioStateChangedEventArgs(false, ""));
                return;
            }

            var transcriptPath = Path.ChangeExtension(wavPath, ".txt");
            await File.WriteAllTextAsync(transcriptPath, transcript);

            var prompt = $"{audio.SystemPrompt.Trim()} See the transcription file: {transcriptPath.Replace('\\', '/')}";
            pty.Write(Encoding.UTF8.GetBytes(prompt));
            await Task.Delay(500);
            pty.Write(Enter);
            AudioStateChanged?.Invoke(new AudioStateChangedEventArgs(false, ""));
        });

        return false;
    }
}
