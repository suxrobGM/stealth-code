using StealthCode.Audio.Models;
using Whisper.net.LibraryLoader;

namespace StealthCode.Audio.Downloads;

/// <summary>A downloadable set of Whisper native files.</summary>
/// <param name="Backend">Setting that selects this pack.</param>
/// <param name="Segment">Runtime folder and pack name.</param>
/// <param name="Library">Whisper runtime provided by the pack.</param>
/// <param name="PackageId">NuGet package name.</param>
/// <param name="Sha512Base64">Expected SHA-512 hash from NuGet.</param>
/// <param name="Accelerator">GPU-specific native file.</param>
/// <param name="DisplayName">Name shown in Settings.</param>
/// <param name="SizeText">Download size shown in Settings.</param>
public sealed record GpuPack(
    GpuBackend Backend,
    string Segment,
    RuntimeLibrary Library,
    string PackageId,
    string Sha512Base64,
    string Accelerator,
    string DisplayName,
    string SizeText)
{
    /// <summary>All native files required for the pack.</summary>
    public string[] Files { get; } =
        ["whisper.dll", "ggml-whisper.dll", "ggml-base-whisper.dll", "ggml-cpu-whisper.dll", Accelerator];

    /// <summary>NuGet download URL for this pack.</summary>
    public string Url { get; } =
        $"https://api.nuget.org/v3-flatcontainer/{PackageId}/{GpuPackCatalog.WhisperVersion}/" +
        $"{PackageId}.{GpuPackCatalog.WhisperVersion}.nupkg";

    /// <summary>Folder holding the installed pack.</summary>
    public string InstallDir { get; } = AudioPaths.PackDir(Segment);

    /// <summary>Folder holding the installed pack's native files.</summary>
    public string NativesDir { get; } = AudioPaths.PackNatives(Segment);

    /// <summary>Whisper.net library search path for this pack.</summary>
    public string LibraryPath { get; } = AudioPaths.PackLibraryPath(Segment);

    /// <summary>Marker file used to detect a failed GPU load.</summary>
    public string MarkerPath { get; } = AudioPaths.GpuLoadMarker(Segment);

    /// <summary>Whether every native file the pack needs is on disk.</summary>
    public bool IsInstalled()
    {
        foreach (var file in Files)
        {
            if (!File.Exists(Path.Combine(NativesDir, file)))
            {
                return false;
            }
        }

        return true;
    }
}

/// <summary>Available GPU packs and their settings values.</summary>
/// <remarks>Pack hashes are tied to <see cref="WhisperVersion"/> and checked against the csproj and NuGet.</remarks>
public static class GpuPackCatalog
{
    public const string WhisperVersion = "1.9.1";

    /// <summary>Path prefix for native files inside the package.</summary>
    public const string ArchivePrefix = "build/win-x64/";

    public static readonly GpuPack[] All =
    [
        new(GpuBackend.Vulkan, "vulkan", RuntimeLibrary.Vulkan,
            "whisper.net.runtime.vulkan",
            "hRYbrj76y09g38wZccGb3DvNiempYMq6Zf6FX/gKzxjS2Ff1JeTZ/XSSuJ0UgU13q/TAsD/Gl/Ctu0PmNcFjdA==",
            "ggml-vulkan-whisper.dll", "Vulkan", "35 MB"),

        new(GpuBackend.Cuda, "cuda", RuntimeLibrary.Cuda,
            "whisper.net.runtime.cuda.windows",
            "J7ZyOZmhgrZCZLGNKNoIxWGcDQYYTsqiDYaHTqAiy/Yo3wN/7UAXqPEA9IICmGwp8X0WZBNkcD86934yALgEZQ==",
            "ggml-cuda-whisper.dll", "CUDA 13", "136 MB"),

        new(GpuBackend.Cuda12, "cuda12", RuntimeLibrary.Cuda12,
            "whisper.net.runtime.cuda12.windows",
            "pzvo217y8nHPcZav82zJ/uHstnsCFpNn6dodrOi2WjwY3j7xyPcBBhGXeRvvpRuZiRY0cuT8BETcY9nKGqVOZw==",
            "ggml-cuda-whisper.dll", "CUDA 12", "238 MB")
    ];

    public static GpuPack? Resolve(GpuBackend backend)
    {
        foreach (var pack in All)
        {
            if (pack.Backend == backend)
            {
                return pack;
            }
        }

        return null;
    }
}
