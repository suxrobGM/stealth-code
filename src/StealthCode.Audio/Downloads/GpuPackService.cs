using System.IO.Compression;
using System.Net.Http;
using System.Security.Cryptography;

namespace StealthCode.Audio.Downloads;

/// <summary>Downloads and installs Whisper's GPU native files.</summary>
/// <remarks>Keeps GPU pack downloads separate from model downloads.</remarks>
public sealed class GpuPackService : IDisposable
{
    // Created on first use; most sessions never install a pack.
    private HttpClient? http;
    private CancellationTokenSource? activeCts;
    private bool isBusy;

    /// <summary>Downloaded bytes and total size.</summary>
    public event Action<long, long>? Progress;

    public void Cancel() => activeCts?.Cancel();

    /// <summary>Downloads and installs a pack. Returns a message for the user.</summary>
    public async Task<string> InstallAsync(GpuPack pack)
    {
        if (isBusy)
        {
            return "A download is already running.";
        }

        isBusy = true;
        activeCts = new CancellationTokenSource();

        try
        {
            await InstallAsync(pack, activeCts.Token);
            return $"{pack.DisplayName} installed.";
        }
        catch (OperationCanceledException)
        {
            return "Download cancelled.";
        }
        catch (Exception ex)
        {
            return ex.Message;
        }
        finally
        {
            Delete(AudioPaths.GpuStaging);
            isBusy = false;
            activeCts?.Dispose();
            activeCts = null;
        }
    }

    /// <summary>Removes an installed pack. Returns false if it is in use.</summary>
    public bool Remove(GpuPack pack) => Delete(pack.InstallDir);

    public void Dispose()
    {
        activeCts?.Cancel();
        activeCts?.Dispose();
        http?.Dispose();
    }

    private async Task InstallAsync(GpuPack pack, CancellationToken token)
    {
        Delete(AudioPaths.GpuStaging);
        Directory.CreateDirectory(AudioPaths.GpuStaging);

        http ??= new HttpClient
        {
            // No timeout; the settings window cancels stalled downloads.
            Timeout = Timeout.InfiniteTimeSpan,
            DefaultRequestHeaders = { { "User-Agent", "StealthCode" } }
        };

        // Hash as the bytes arrive so the archive is never read a second time.
        var archivePath = Path.Combine(AudioPaths.GpuStaging, "pack.nupkg");
        using var hasher = IncrementalHash.CreateHash(HashAlgorithmName.SHA512);
        await FileDownloader.DownloadAsync(http, pack.Url, archivePath, Progress, hasher, token);

        var hash = Convert.ToBase64String(hasher.GetHashAndReset());
        if (!string.Equals(hash, pack.Sha512Base64, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("The download did not match the published checksum.");
        }

        // Extract beside the final folder, then move it into place.
        var stageDir = Path.Combine(AudioPaths.GpuStaging, pack.Segment);
        var nativesDir = AudioPaths.NativesIn(stageDir, pack.Segment);
        Directory.CreateDirectory(nativesDir);

        using (var archive = ZipFile.OpenRead(archivePath))
        {
            foreach (var entry in archive.Entries)
            {
                if (!entry.FullName.StartsWith(GpuPackCatalog.ArchivePrefix, StringComparison.OrdinalIgnoreCase)
                    || !entry.Name.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                // Use only the file name to prevent archive paths escaping this folder.
                entry.ExtractToFile(Path.Combine(nativesDir, Path.GetFileName(entry.Name)), true);
            }
        }

        List<string>? missing = null;
        foreach (var file in pack.Files)
        {
            if (!File.Exists(Path.Combine(nativesDir, file)))
            {
                (missing ??= []).Add(file);
            }
        }

        if (missing is not null)
        {
            throw new InvalidOperationException($"The download was missing {string.Join(", ", missing)}.");
        }

        if (Directory.Exists(pack.InstallDir) && !Delete(pack.InstallDir))
        {
            throw new InvalidOperationException($"{pack.DisplayName} is in use. Restart the app and try again.");
        }

        // Move the completed folder into place.
        Directory.Move(stageDir, pack.InstallDir);
    }

    /// <summary>Removes a file or folder. Returns false if it is in use.</summary>
    private static bool Delete(string path)
    {
        try
        {
            if (Directory.Exists(path))
            {
                Directory.Delete(path, true);
            }
            else if (File.Exists(path))
            {
                File.Delete(path);
            }

            return true;
        }
        catch
        {
            return false;
        }
    }
}
