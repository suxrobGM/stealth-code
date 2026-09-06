using System.Net.Http;

namespace StealthCode.Audio.Downloads;

/// <summary>
/// Downloads Whisper model files from Hugging Face with progress reporting.
/// </summary>
public sealed class ModelDownloadService : IDisposable
{
    private const string BaseUrl = "https://huggingface.co/ggerganov/whisper.cpp/resolve/main";

    private readonly HttpClient http = new() { Timeout = TimeSpan.FromMinutes(30) };
    private CancellationTokenSource? activeCts;

    public event Action<long, long>? DownloadProgress;

    public bool IsDownloading { get; private set; }

    public async Task<bool> DownloadAsync(string modelFileName, string targetPath,
        CancellationToken cancellationToken = default)
    {
        if (IsDownloading)
        {
            return false;
        }

        IsDownloading = true;
        activeCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

        try
        {
            var dir = Path.GetDirectoryName(targetPath);
            if (!string.IsNullOrEmpty(dir))
            {
                Directory.CreateDirectory(dir);
            }

            var url = $"{BaseUrl}/{modelFileName}";
            var tempPath = targetPath + ".download";

            await FileDownloader.DownloadAsync(http, url, tempPath, DownloadProgress, null, activeCts.Token);

            File.Move(tempPath, targetPath, true);
            return true;
        }
        catch (OperationCanceledException)
        {
            CleanupTempFile(targetPath);
            return false;
        }
        catch
        {
            CleanupTempFile(targetPath);
            return false;
        }
        finally
        {
            IsDownloading = false;
            activeCts = null;
        }
    }

    public void CancelDownload() => activeCts?.Cancel();

    public static bool ModelExists(string modelPath) => File.Exists(modelPath);

    public void Dispose() => http.Dispose();

    private static void CleanupTempFile(string targetPath)
    {
        try { File.Delete(targetPath + ".download"); }
        catch { /* Ignore */ }
    }
}
