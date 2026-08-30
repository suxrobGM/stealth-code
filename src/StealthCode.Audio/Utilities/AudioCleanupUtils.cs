namespace StealthCode.Audio.Utilities;

public static class AudioCleanupUtils
{
    /// <summary>Deletes recordings left behind by previous sessions, which are of no use once transcribed.</summary>
    public static void CleanupOldRecordings()
    {
        if (!Directory.Exists(AudioPaths.Captures))
        {
            return;
        }

        try
        {
            foreach (var file in Directory.GetFiles(AudioPaths.Captures, $"{AudioPaths.CapturePrefix}*.*"))
            {
                File.Delete(file);
            }
        }
        catch
        {
            // Ignore cleanup errors
        }
    }
}
