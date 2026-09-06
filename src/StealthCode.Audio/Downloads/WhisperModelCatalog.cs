namespace StealthCode.Audio.Downloads;

/// <summary>A downloadable Whisper model.</summary>
/// <param name="FileName">File name on Hugging Face and on disk.</param>
/// <param name="DisplayName">Name shown in Settings.</param>
/// <param name="SizeText">Download size shown in Settings.</param>
/// <param name="Hint">Short accuracy/speed hint shown in Settings.</param>
public sealed record WhisperModel(string FileName, string DisplayName, string SizeText, string Hint)
{
    /// <summary>Full path of the model file in the models folder.</summary>
    public string Path => AudioPaths.ModelFile(FileName);

    /// <summary>Text shown for this model in the picker.</summary>
    public string Label => $"{DisplayName} - {SizeText}, {Hint}";
}

/// <summary>Available Whisper models and their settings values.</summary>
public static class WhisperModelCatalog
{
    public static readonly WhisperModel[] All =
    [
        new("ggml-base.bin", "base", "148 MB", "fast, CPU"),
        new("ggml-base.en.bin", "base.en", "148 MB", "fast, CPU, English only"),
        new("ggml-small.bin", "small", "488 MB", "better accuracy"),
        new("ggml-small.en.bin", "small.en", "488 MB", "better accuracy, English only"),
        new("ggml-large-v3-turbo-q5_0.bin", "large-v3-turbo q5", "574 MB", "best, GPU recommended"),
        new("ggml-large-v3-turbo.bin", "large-v3-turbo", "1.6 GB", "best, GPU, full precision")
    ];

    public static WhisperModel? Resolve(string modelPath)
    {
        var fileName = Path.GetFileName(modelPath);

        foreach (var model in All)
        {
            if (string.Equals(model.FileName, fileName, StringComparison.OrdinalIgnoreCase))
            {
                return model;
            }
        }

        return null;
    }
}
