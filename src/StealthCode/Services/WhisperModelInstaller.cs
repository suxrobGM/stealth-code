using StealthCode.Audio.Downloads;

namespace StealthCode.Services;

/// <summary>
/// Owns the Whisper model download. A download outlives the settings panel that starts it, so the state lives
/// here and both view models observe it rather than passing messages around.
/// </summary>
public sealed class WhisperModelInstaller(
    ModelDownloadService downloadService,
    SettingsService settingsService)
{
    public bool IsDownloading => downloadService.IsDownloading;

    /// <summary>Bytes downloaded and expected. Each view model formats it for its own surface.</summary>
    public event Action<long, long>? Progress;

    public event Action<bool>? Completed;

    public async Task InstallAsync()
    {
        if (IsDownloading)
        {
            return;
        }

        var modelPath = settingsService.Settings.Audio.ModelPath;

        if (ModelDownloadService.ModelExists(modelPath))
        {
            Completed?.Invoke(true);
            return;
        }

        downloadService.DownloadProgress += OnProgress;

        try
        {
            var success = await downloadService.DownloadAsync(Path.GetFileName(modelPath), modelPath);
            Completed?.Invoke(success);
        }
        finally
        {
            downloadService.DownloadProgress -= OnProgress;
        }
    }

    public void Cancel() => downloadService.CancelDownload();

    private void OnProgress(long downloaded, long total) => Progress?.Invoke(downloaded, total);
}
