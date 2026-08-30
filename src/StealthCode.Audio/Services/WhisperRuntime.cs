using Whisper.net.LibraryLoader;

namespace StealthCode.Audio.Services;

/// <summary>Chooses the CPU or graphics-card version of Whisper, once per run.</summary>
/// <remarks>
/// Whisper.net normally tries the graphics card first and moves on to the CPU if that fails. That recovery
/// does not work: once one attempt has failed, the next one closes the whole app rather than reporting an
/// error we could handle. So a single choice is made up front. The graphics card is only used if the user
/// asks for it, and asking writes a small marker file that is deleted once the model has loaded. Finding
/// that file still there next time means the app closed while loading, so this run uses the CPU instead.
/// </remarks>
internal static class WhisperRuntime
{
    private static bool configured;
    private static bool usingGpu;

    /// <summary>True when the user asked for the graphics card but it closed the app last time.</summary>
    public static bool GpuDisabledAfterCrash { get; private set; }

    /// <summary>
    /// Makes the choice. Only the first call counts, because the underlying library loads once and cannot
    /// be swapped afterwards, so a changed setting only applies the next time the app starts.
    /// </summary>
    public static void Configure(bool useGpu)
    {
        if (configured)
        {
            return;
        }

        configured = true;

        // In a published build Whisper.net's own search comes back empty and it looks in whichever folder
        // the app happens to be started from. Point it at the app's own folder instead.
        RuntimeOptions.LibraryPath = AppContext.BaseDirectory;

        usingGpu = useGpu && ShouldTryGpu();
        RuntimeOptions.RuntimeLibraryOrder = [usingGpu ? RuntimeLibrary.Cuda : RuntimeLibrary.Cpu];
    }

    /// <summary>Call once the model has loaded, which shows the choice worked.</summary>
    public static void MarkLoadSucceeded()
    {
        if (usingGpu)
        {
            DeleteMarker();
        }
    }

    /// <summary>
    /// Returns false when the marker file from the last attempt is still there. Otherwise leaves a marker
    /// behind, so an app that closes while loading can be recognised on the next run.
    /// </summary>
    private static bool ShouldTryGpu()
    {
        try
        {
            if (File.Exists(AudioPaths.GpuLoadMarker))
            {
                GpuDisabledAfterCrash = true;
                DeleteMarker();
                return false;
            }

            Directory.CreateDirectory(Path.GetDirectoryName(AudioPaths.GpuLoadMarker)!);
            File.WriteAllText(AudioPaths.GpuLoadMarker, "loading cuda");
            return true;
        }
        catch
        {
            // With no marker file there is no way to tell whether the last attempt closed the app.
            return false;
        }
    }

    private static void DeleteMarker()
    {
        try
        {
            File.Delete(AudioPaths.GpuLoadMarker);
        }
        catch
        {
            // A leftover file only costs one run on the CPU.
        }
    }
}
