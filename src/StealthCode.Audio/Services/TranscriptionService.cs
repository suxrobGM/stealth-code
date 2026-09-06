using System.Text;
using StealthCode.Audio.Models;
using Whisper.net;

namespace StealthCode.Audio.Services;

/// <summary>Transcribes 16 kHz mono audio with Whisper.net and keeps the loaded model.</summary>
public sealed class TranscriptionService : IDisposable
{
    private const int MinChunkSamples = AudioConverter.WhisperSampleRate * 3 / 2;
    private const string DefaultLanguage = "en";

    private WhisperFactory? factory;
    private string? loadedModelPath;
    private string language = DefaultLanguage;

    /// <summary>True when a GPU runtime was skipped and the CPU used instead.</summary>
    public bool GpuFellBack { get; private set; }

    /// <summary>Loads the model, reloading it when the path changed. Returns an error message on failure.</summary>
    public Task<string?> LoadAsync(AudioSettings settings) => Task.Run(() => Load(settings));

    /// <summary>Runs silence through the model so the first real chunk is not slowed by initialization.</summary>
    public Task WarmUpAsync(CancellationToken ct) => TranscribeAsync(new float[MinChunkSamples], null, ct);

    /// <summary>Transcribes one chunk, using <paramref name="prompt"/> as context for the decoder.</summary>
    public async Task<string> TranscribeAsync(float[] samples16k, string? prompt, CancellationToken ct)
    {
        if (factory is null || samples16k.Length == 0)
        {
            return string.Empty;
        }

        var samples = samples16k;
        if (samples.Length < MinChunkSamples)
        {
            samples = new float[MinChunkSamples];
            samples16k.CopyTo(samples, 0);
        }

        var builder = language == "auto"
            ? factory.CreateBuilder().WithLanguageDetection()
            : factory.CreateBuilder().WithLanguage(language);

        builder = builder
            .WithThreads(Math.Min(Environment.ProcessorCount, 8))
            .WithNoSpeechThreshold(0.6f)
            .WithTemperatureInc(0f);

        if (!string.IsNullOrWhiteSpace(prompt))
        {
            builder = builder.WithPrompt(prompt);
        }

        var text = new StringBuilder();

        // The greedy strategy comes last because it returns a different builder type.
        await using var processor = builder.WithGreedySamplingStrategy().Build();

        await foreach (var segment in processor.ProcessAsync(samples, ct))
        {
            var cleaned = TranscriptFilter.Clean(segment.Text, segment.NoSpeechProbability);
            if (cleaned.Length == 0)
            {
                continue;
            }

            if (text.Length > 0)
            {
                text.Append(' ');
            }

            text.Append(cleaned);
        }

        return text.ToString();
    }

    public void Dispose() => Unload();

    private string? Load(AudioSettings settings)
    {
        language = NormalizeLanguage(settings.Language);

        if (string.IsNullOrWhiteSpace(settings.ModelPath) || !File.Exists(settings.ModelPath))
        {
            return "Whisper model not found. Set the model path in Settings > Audio.";
        }

        if (factory is not null && loadedModelPath == settings.ModelPath)
        {
            return null;
        }

        Unload();
        WhisperRuntime.Configure(settings.GpuBackend);

        try
        {
            factory = WhisperFactory.FromPath(settings.ModelPath);

            // The model loaded, so clear the GPU load marker.
            WhisperRuntime.MarkLoadSucceeded();
            loadedModelPath = settings.ModelPath;
            GpuFellBack = settings.GpuBackend != GpuBackend.None && WhisperRuntime.GpuDisabledAfterCrash;
            return null;
        }
        catch (Exception ex)
        {
            Unload();

            // Add context because the raw error is not useful by itself.
            return $"Failed to load the Whisper model. {ex.Message}";
        }
    }

    private static string NormalizeLanguage(string? value)
    {
        var lang = value?.Trim().ToLowerInvariant();
        if (lang == "auto")
        {
            return lang;
        }

        return lang is { Length: 2 } && char.IsAsciiLetter(lang[0]) && char.IsAsciiLetter(lang[1])
            ? lang
            : DefaultLanguage;
    }

    private void Unload()
    {
        factory?.Dispose();
        factory = null;
        loadedModelPath = null;
    }
}
