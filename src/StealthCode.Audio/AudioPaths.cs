using StealthCode.Audio.Services;

namespace StealthCode.Audio;

/// <summary>Paths used by the audio module in the app data folder.</summary>
internal static class AudioPaths
{
    private static readonly string Root = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "StealthCode");

    /// <summary>Where recordings are written, as <c>audio_*.wav</c>.</summary>
    public static readonly string Captures = Path.Combine(Root, "captures");

    /// <summary>Prefix used for audio recording files.</summary>
    public const string CapturePrefix = "audio_";

    /// <summary>Where the Whisper model is downloaded to unless the user picks somewhere else.</summary>
    public static readonly string DefaultModel = Path.Combine(Root, "models", "ggml-base.bin");

    /// <summary>Folder for downloaded GPU packs, outside the app folder.</summary>
    public static readonly string GpuRoot = Path.Combine(Root, "gpu");

    /// <summary>Temporary folder used while installing a pack.</summary>
    public static readonly string GpuStaging = Path.Combine(GpuRoot, ".staging");

    /// <summary>Folder for a pack, named by runtime and Whisper version.</summary>
    public static string PackDir(string segment) =>
        Path.Combine(GpuRoot, $"{segment}-{GpuPackCatalog.WhisperVersion}");

    /// <summary>Folder containing a pack's native files, under any root.</summary>
    public static string NativesIn(string root, string segment) =>
        Path.Combine(root, "runtimes", segment, "win-x64");

    /// <summary>Folder containing an installed pack's native files.</summary>
    public static string PackNatives(string segment) => NativesIn(PackDir(segment), segment);

    /// <summary>Whisper.net library search path. It must end with a separator.</summary>
    public static string PackLibraryPath(string segment) => PackDir(segment) + Path.DirectorySeparatorChar;

    /// <summary>Per-runtime marker used to detect a failed GPU load.</summary>
    public static string GpuLoadMarker(string segment) => Path.Combine(Root, $"gpu-probe-{segment}.lock");
}
