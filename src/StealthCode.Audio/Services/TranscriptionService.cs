using StealthCode.Audio.Models;
using Whisper.net;

namespace StealthCode.Audio.Services;

/// <summary>
/// Turns recorded audio into text with Whisper.net (whisper.cpp running on this machine), keeping the
/// loaded model between calls. <see cref="WhisperRuntime"/> picks CPU or graphics card before the first load.
/// </summary>
public sealed class TranscriptionService : IDisposable
{
    private WhisperFactory? factory;
    private WhisperProcessor? processor;
    private string? loadedModelPath;

    /// <summary>
    /// Transcribes a recording. Clears <see cref="AudioSettings.UseGpu"/> when the graphics card was asked
    /// for but skipped because it closed the app while loading, so the setting stops showing as on. The
    /// caller only has to save the settings when they come back changed.
    /// </summary>
    public async Task<string> TranscribeAsync(string wavPath, AudioSettings settings)
    {
        if (string.IsNullOrWhiteSpace(settings.ModelPath) || !File.Exists(settings.ModelPath))
        {
            return "[Error: Whisper model not found. Configure the model path in Settings > Audio.]";
        }

        if (!File.Exists(wavPath))
        {
            return "[Error: Audio file not found.]";
        }

        var loadError = EnsureProcessor(settings.ModelPath, settings.UseGpu);

        if (settings.UseGpu && WhisperRuntime.GpuDisabledAfterCrash)
        {
            settings.UseGpu = false;
        }

        if (processor is null)
        {
            // "Failed to load" on its own gives the user nothing to act on.
            return $"[Error: Failed to load Whisper model. {loadError}]";
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

        return segments.Count > 0 ? string.Join(" ", segments) : "[No speech detected in recording.]";
    }

    public void Dispose()
    {
        processor?.Dispose();
        factory?.Dispose();
    }

    /// <summary>Loads the model unless it is already loaded. Returns null, or why it could not load.</summary>
    private string? EnsureProcessor(string modelPath, bool useGpu)
    {
        if (processor is not null && loadedModelPath == modelPath)
        {
            return null;
        }

        Unload();
        WhisperRuntime.Configure(useGpu);

        try
        {
            factory = WhisperFactory.FromPath(modelPath);
            processor = factory.CreateBuilder()
                .WithLanguage("auto")
                .Build();

            // The model loaded, so the marker file left by a graphics-card attempt can go.
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
