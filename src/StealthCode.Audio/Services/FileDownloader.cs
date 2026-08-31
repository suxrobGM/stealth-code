using System.Diagnostics;
using System.Net.Http;
using System.Security.Cryptography;

namespace StealthCode.Audio.Services;

/// <summary>Streams an HTTP download to a file, with progress and optional hashing.</summary>
internal static class FileDownloader
{
    private const int BufferSize = 81920;

    /// <summary>How often progress is reported, so a long download does not flood the UI.</summary>
    private static readonly TimeSpan ProgressInterval = TimeSpan.FromMilliseconds(100);

    /// <summary>Downloads <paramref name="url"/> to <paramref name="targetPath"/>.</summary>
    /// <param name="progress">Given downloaded bytes and total size, or -1 when the size is unknown.</param>
    /// <param name="hasher">Fed the bytes as they arrive, so the file never has to be read back.</param>
    public static async Task DownloadAsync(HttpClient http, string url, string targetPath,
        Action<long, long>? progress, IncrementalHash? hasher, CancellationToken token)
    {
        using var response = await http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, token);
        response.EnsureSuccessStatusCode();

        var total = response.Content.Headers.ContentLength ?? -1;

        await using var contentStream = await response.Content.ReadAsStreamAsync(token);
        await using var fileStream = File.Create(targetPath);

        var buffer = new byte[BufferSize];
        var lastReport = Stopwatch.GetTimestamp();
        long downloaded = 0;
        int read;

        while ((read = await contentStream.ReadAsync(buffer, token)) > 0)
        {
            await fileStream.WriteAsync(buffer.AsMemory(0, read), token);
            hasher?.AppendData(buffer, 0, read);
            downloaded += read;

            if (Stopwatch.GetElapsedTime(lastReport) >= ProgressInterval)
            {
                lastReport = Stopwatch.GetTimestamp();
                progress?.Invoke(downloaded, total);
            }
        }

        await fileStream.FlushAsync(token);
        progress?.Invoke(downloaded, total);
    }
}
