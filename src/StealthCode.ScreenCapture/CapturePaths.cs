namespace StealthCode.ScreenCapture;

/// <summary>
/// The ScreenCapture module's corner of the app data folder. Kept here so the module owns its own layout
/// and no other module has to know where its files live or what they are called.
/// </summary>
internal static class CapturePaths
{
    /// <summary>Where screenshots are written, as <c>capture_*.png</c>.</summary>
    public static readonly string Captures = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "StealthCode", "captures");

    /// <summary>Prefix every screenshot shares, so old ones can be told from other modules' files.</summary>
    public const string CapturePrefix = "capture_";
}
