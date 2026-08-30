namespace StealthCode.Audio;

/// <summary>
/// The Audio module's corner of the app data folder. Kept here so the module owns its own layout and no
/// other module has to know where its files live or what they are called.
/// </summary>
internal static class AudioPaths
{
    private static readonly string Root = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "StealthCode");

    /// <summary>Where recordings are written, as <c>audio_*.wav</c>.</summary>
    public static readonly string Captures = Path.Combine(Root, "captures");

    /// <summary>Prefix every recording shares, so old ones can be told from other modules' files.</summary>
    public const string CapturePrefix = "audio_";

    /// <summary>Where the Whisper model is downloaded to unless the user picks somewhere else.</summary>
    public static readonly string DefaultModel = Path.Combine(Root, "models", "ggml-base.bin");
}
