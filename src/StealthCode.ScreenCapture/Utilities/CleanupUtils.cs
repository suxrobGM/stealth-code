namespace StealthCode.ScreenCapture.Utilities;

public static class CleanupUtils
{
    /// <summary>Deletes screenshots left behind by previous sessions to free up disk space.</summary>
    public static void CleanupOldCaptures()
    {
        if (!Directory.Exists(CapturePaths.Captures))
        {
            return;
        }

        try
        {
            foreach (var file in Directory.GetFiles(CapturePaths.Captures, $"{CapturePaths.CapturePrefix}*.*"))
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
