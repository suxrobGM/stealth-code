using StealthCode.Audio.Models;
using Whisper.net;

namespace StealthCode.Audio.Services;

/// <summary>Result of transcribing one recording.</summary>
/// <param name="Text">Transcript text, or empty on failure.</param>
/// <param name="Error">Error message, if transcription failed.</param>
/// <param name="GpuFellBack">Whether a GPU runtime was skipped and the CPU used instead.</param>
public sealed record TranscriptionResult(string Text, string? Error = null, bool GpuFellBack = false)
{
    /// <summary>Whether transcription succeeded.</summary>
    public bool Ok => Error is null;

    public static TranscriptionResult Success(string text, bool gpuFellBack = false) =>
        new(text, null, gpuFellBack);

    public static TranscriptionResult Failure(string error, bool gpuFellBack = false) =>
        new(string.Empty, error, gpuFellBack);
}

/// <summary>Converts recorded audio to text with Whisper.net and reuses the loaded model.</summary>
public sealed class TranscriptionService : IDisposable
{
    private WhisperFactory? factory;
    private WhisperProcessor? processor;
    private string? loadedModelPath;

    /// <summary>Transcribes a recording, reporting any GPU fallback in the result.</summary>
    public async Task<TranscriptionResult> TranscribeAsync(string wavPath, AudioSettings settings)
    {
        if (string.IsNullOrWhiteSpace(settings.ModelPath) || !File.Exists(settings.ModelPath))
        {
            return TranscriptionResult.Failure("Whisper model not found. Set the model path in Settings > Audio.");
        }

        if (!File.Exists(wavPath))
        {
            return TranscriptionResult.Failure("The recording could not be found.");
        }

        var loadError = EnsureProcessor(settings.ModelPath, settings.GpuBackend);
        var gpuFellBack = settings.GpuBackend != GpuBackend.None && WhisperRuntime.GpuDisabledAfterCrash;

        if (processor is null)
        {
            // Add context because the raw error is not useful by itself.
            return TranscriptionResult.Failure($"Failed to load the Whisper model. {loadError}", gpuFellBack);
        }

        await using var fileStream = File.OpenRead(wavPath);
        var segments = new List<string>();

        await foreach (var segment in processor.ProcessAsync(fileStream))
        {
            if (!string.IsNullOrWhiteSpace(segment.Text))
            {
                segments.Add(segment.Text.Trim());
            }
        }

        return segments.Count > 0
            ? TranscriptionResult.Success(string.Join(" ", segments), gpuFellBack)
            : TranscriptionResult.Failure("No speech was found in the recording.", gpuFellBack);
    }

    public void Dispose()
    {
        processor?.Dispose();
        factory?.Dispose();
    }

    /// <summary>Loads the model if needed. Returns an error message on failure.</summary>
    private string? EnsureProcessor(string modelPath, GpuBackend backend)
    {
        if (processor is not null && loadedModelPath == modelPath)
        {
            return null;
        }

        Unload();
        WhisperRuntime.Configure(backend);

        try
        {
            factory = WhisperFactory.FromPath(modelPath);
            processor = factory.CreateBuilder()
                .WithLanguage("auto")
                .Build();

            // The model loaded, so clear the GPU load marker.
            WhisperRuntime.MarkLoadSucceeded();
            loadedModelPath = modelPath;
            return null;
        }
        catch (Exception ex)
        {
            Unload();
            return ex.Message;
        }
    }

    private void Unload()
    {
        processor?.Dispose();
        factory?.Dispose();
        processor = null;
        factory = null;
        loadedModelPath = null;
    }
}
