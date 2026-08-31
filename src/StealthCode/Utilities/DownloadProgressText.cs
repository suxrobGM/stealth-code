namespace StealthCode.Utilities;

/// <summary>Formats download progress for the status lines that report it.</summary>
internal static class DownloadProgressText
{
    private const double BytesPerMb = 1024 * 1024;

    /// <summary>Reads as "12.3/142 MB (8%)", or "12.3 MB" when the total size is unknown.</summary>
    public static string Format(long downloaded, long total) => total > 0
        ? $"{downloaded / BytesPerMb:F1}/{total / BytesPerMb:F0} MB ({downloaded * 100 / total}%)"
        : $"{downloaded / BytesPerMb:F1} MB";
}
