using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using StealthCode.Messages;
using StealthCode.Updater.Models;
using StealthCode.Updater.Services;

namespace StealthCode.ViewModels.Settings;

/// <summary>Current version, update check, and the download-and-restart flow.</summary>
public sealed partial class UpdateSettingsViewModel(UpdateService updateService) : ViewModelBase
{
    private GitHubRelease? pendingRelease;

    public string VersionText { get; } = $"v{UpdateService.CurrentVersion}";

    [ObservableProperty]
    public partial string? StatusText { get; set; }

    [ObservableProperty]
    public partial bool IsBusy { get; set; }

    [ObservableProperty]
    public partial bool HasPendingRelease { get; set; }

    /// <summary>Names the version and the restart.</summary>
    [ObservableProperty]
    public partial string InstallButtonText { get; set; } = "Install and restart";

    [RelayCommand]
    private async Task CheckForUpdate()
    {
        if (IsBusy)
        {
            return;
        }

        IsBusy = true;
        StatusText = "Checking...";

        try
        {
            pendingRelease = await updateService.CheckForUpdateAsync();
        }
        catch (Exception ex)
        {
            StatusText = $"Could not check for updates: {ex.Message}";
            return;
        }
        finally
        {
            IsBusy = false;
        }

        if (pendingRelease is null)
        {
            StatusText = "You are on the latest version";
            return;
        }

        StatusText = $"{pendingRelease.TagName} is available";
        InstallButtonText = $"Install {pendingRelease.TagName} and restart";
        HasPendingRelease = true;
        WeakReferenceMessenger.Default.Send(new UpdateAvailableMessage(true));
    }

    [RelayCommand]
    private async Task InstallUpdate()
    {
        if (IsBusy || pendingRelease is not { } release)
        {
            return;
        }

        IsBusy = true;
        StatusText = "Downloading...";

        updateService.DownloadProgress += OnDownloadProgress;
        var success = await updateService.DownloadAndApplyAsync(release);
        updateService.DownloadProgress -= OnDownloadProgress;

        if (success)
        {
            StatusText = "Restarting...";
            UpdateService.LaunchUpdateAndExit();
            return;
        }

        IsBusy = false;
        StatusText = "Update failed. Try again.";
    }

    private void OnDownloadProgress(long downloaded, long total)
    {
        if (total <= 0)
        {
            return;
        }

        var percent = (int)(downloaded * 100 / total);
        Dispatcher.UIThread.Post(() => StatusText = $"Downloading... {percent}%");
    }
}
