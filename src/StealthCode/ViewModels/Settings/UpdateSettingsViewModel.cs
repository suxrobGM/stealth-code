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
    private const string CheckLabel = "Check for Updates";
    private const string UpdateLabel = "Update & Restart";

    private GitHubRelease? pendingRelease;

    public string VersionText { get; } = $"v{UpdateService.CurrentVersion}";

    [ObservableProperty]
    public partial string? StatusText { get; set; }

    [ObservableProperty]
    public partial bool IsUpdating { get; set; }

    [ObservableProperty]
    public partial string ButtonText { get; set; } = CheckLabel;

    [RelayCommand]
    private async Task CheckForUpdate()
    {
        if (IsUpdating)
        {
            return;
        }

        // A second press once a release is known applies it.
        if (pendingRelease is { } release)
        {
            await ApplyUpdateAsync(release);
            return;
        }

        ButtonText = "Checking...";
        pendingRelease = await updateService.CheckForUpdateAsync();

        if (pendingRelease is null)
        {
            StatusText = "You're on the latest version";
            ButtonText = CheckLabel;
            return;
        }

        StatusText = $"New version available: {pendingRelease.TagName}";
        ButtonText = UpdateLabel;
        WeakReferenceMessenger.Default.Send(new UpdateAvailableMessage(true));
    }

    private async Task ApplyUpdateAsync(GitHubRelease release)
    {
        IsUpdating = true;
        ButtonText = "Downloading...";

        updateService.DownloadProgress += OnDownloadProgress;
        var success = await updateService.DownloadAndApplyAsync(release);
        updateService.DownloadProgress -= OnDownloadProgress;

        if (success)
        {
            ButtonText = "Restarting...";
            UpdateService.LaunchUpdateAndExit();
            return;
        }

        IsUpdating = false;
        ButtonText = UpdateLabel;
        StatusText = "Update failed. Try again.";
    }

    private void OnDownloadProgress(long downloaded, long total)
    {
        if (total <= 0)
        {
            return;
        }

        var percent = (int)(downloaded * 100 / total);
        Dispatcher.UIThread.Post(() => ButtonText = $"Downloading... {percent}%");
    }
}
