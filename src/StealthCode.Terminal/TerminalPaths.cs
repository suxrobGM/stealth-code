namespace StealthCode.Terminal;

/// <summary>
/// The Terminal module's corner of the filesystem. The terminal document itself is served from memory, so
/// the only thing that needs a path is the WebView host's profile.
/// </summary>
public static class TerminalPaths
{
    private static readonly string ProfileRoot = Path.Combine(Path.GetTempPath(), "StealthCode", "webview");

    /// <summary>
    /// Scratch profile for the WebView host. WebView2 insists on a writable user-data folder, so it gets a
    /// per-process one under the temp directory that <see cref="CleanupProfiles"/> removes, rather than a
    /// browser profile that outlives the session next to the executable.
    /// </summary>
    public static readonly string WebViewProfile = Path.Combine(ProfileRoot, Environment.ProcessId.ToString());

    /// <summary>
    /// Deletes WebView profiles, including any a previous session was killed before it could clean up. The
    /// profile belonging to a live WebView is locked, so failures here are expected and ignored.
    /// </summary>
    public static void CleanupProfiles()
    {
        if (!Directory.Exists(ProfileRoot))
        {
            return;
        }

        foreach (var directory in Directory.GetDirectories(ProfileRoot))
        {
            try
            {
                Directory.Delete(directory, true);
            }
            catch
            {
                // In use by this or another running instance
            }
        }
    }
}
