using StealthCode.Audio.Downloads;
using StealthCode.Audio.Models;
using Whisper.net.LibraryLoader;

namespace StealthCode.Audio.Transcription;

/// <summary>Selects the Whisper runtime and library path.</summary>
/// <remarks>Skips a GPU runtime after a failed load to protect the app.</remarks>
internal static class WhisperRuntime
{
    private static GpuBackend? configuredFor;
    private static GpuPack? activePack;

    /// <summary>True when a GPU load was skipped after a previous crash.</summary>
    public static bool GpuDisabledAfterCrash { get; private set; }

    /// <summary>Selects a runtime until the native library loads.</summary>
    public static void Configure(GpuBackend backend)
    {
        if (configuredFor == backend || RuntimeOptions.LoadedLibrary is not null)
        {
            return;
        }

        configuredFor = backend;
        GpuDisabledAfterCrash = false;
        activePack = null;

        var pack = GpuPackCatalog.Resolve(backend);

        // Fall back to CPU when the GPU pack is missing or unsafe.
        if (pack is null || !pack.IsInstalled() || !ShouldTry(pack))
        {
            RuntimeOptions.LibraryPath = AppContext.BaseDirectory;
            RuntimeOptions.RuntimeLibraryOrder = [RuntimeLibrary.Cpu];
            return;
        }

        activePack = pack;
        RuntimeOptions.LibraryPath = pack.LibraryPath;
        RuntimeOptions.RuntimeLibraryOrder = [pack.Library];
    }

    /// <summary>Clears the GPU marker after a successful model load.</summary>
    public static void MarkLoadSucceeded()
    {
        if (activePack is { } pack)
        {
            DeleteMarker(pack);
        }
    }

    /// <summary>Checks whether a previous GPU load failed.</summary>
    private static bool ShouldTry(GpuPack pack)
    {
        try
        {
            if (File.Exists(pack.MarkerPath))
            {
                GpuDisabledAfterCrash = true;
                DeleteMarker(pack);
                return false;
            }

            Directory.CreateDirectory(Path.GetDirectoryName(pack.MarkerPath)!);
            File.WriteAllText(pack.MarkerPath, pack.Segment);
            return true;
        }
        catch
        {
            // Without a marker, skip the GPU to protect the app.
            return false;
        }
    }

    private static void DeleteMarker(GpuPack pack)
    {
        try
        {
            File.Delete(pack.MarkerPath);
        }
        catch
        {
            // A leftover marker only affects one run.
        }
    }
}
