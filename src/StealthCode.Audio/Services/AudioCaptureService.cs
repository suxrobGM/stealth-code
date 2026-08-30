using System.Runtime.Versioning;

namespace StealthCode.Audio.Services;

/// <summary>
/// Orchestrates loopback audio capture: recording, conversion, and WAV export.
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class AudioCaptureService : IDisposable
{
    private readonly WasapiLoopbackCapture loopback = new();
    private MemoryStream? capturedPcm;

    public bool IsRecording => capturedPcm is not null;
    public string? LastError => loopback.LastError;

    public bool StartCapture()
    {
        if (IsRecording)
        {
            return false;
        }

        capturedPcm = new MemoryStream();
        loopback.Start(capturedPcm);
        return true;
    }

    /// <summary>
    /// Stops recording and returns the captured audio as a WAV file path, or null if no data.
    /// </summary>
    public string? StopCapture()
    {
        if (!IsRecording)
        {
            return null;
        }

        loopback.Stop();

        var pcmData = capturedPcm!.ToArray();
        capturedPcm.Dispose();
        capturedPcm = null;

        if (pcmData.Length == 0 || loopback.Format is null)
        {
            return null;
        }

        var samples = AudioConverter.ToWhisperFormat(pcmData, loopback.Format);
        if (samples.Length == 0)
        {
            return null;
        }

        Directory.CreateDirectory(AudioPaths.Captures);
        var wavPath = Path.Combine(
            AudioPaths.Captures, $"{AudioPaths.CapturePrefix}{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}.wav");
        AudioConverter.WriteWav(wavPath, samples, 16000);

        return wavPath;
    }

    public void Dispose()
    {
        loopback.Stop();
        capturedPcm?.Dispose();
    }
}
