using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using StealthCode.Audio.Downloads;
using StealthCode.Audio.Models;
using StealthCode.Services;
using StealthCode.Utilities;

namespace StealthCode.ViewModels.Settings;

/// <summary>Which runtime Whisper uses, and the on-demand download of its native pack.</summary>
public sealed partial class GpuPackSettingsViewModel(
    SettingsService settingsService,
    GpuPackService gpuPackService) : ViewModelBase
{
    private bool isLoading;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasPack))]
    public partial GpuBackend SelectedBackend { get; set; }

    [ObservableProperty]
    public partial string? StatusText { get; set; }

    [ObservableProperty]
    public partial string ButtonText { get; set; } = "Install";

    /// <summary>Which of Install or Remove applies.</summary>
    [ObservableProperty]
    public partial bool IsInstalled { get; set; }

    [ObservableProperty]
    public partial bool IsBusy { get; set; }

    /// <summary>Whether the selected backend needs a pack, and so has buttons to show.</summary>
    public bool HasPack => SelectedPack is not null;

    private GpuPack? SelectedPack => GpuPackCatalog.Resolve(SelectedBackend);

    public void Load()
    {
        isLoading = true;

        try
        {
            SelectedBackend = settingsService.Settings.Audio.GpuBackend;
            StatusText = Refresh();
        }
        finally
        {
            isLoading = false;
        }
    }

    [RelayCommand]
    private void Remove()
    {
        if (SelectedPack is not { } pack)
        {
            return;
        }

        var removed = gpuPackService.Remove(pack);
        var status = Refresh();
        StatusText = removed ? status : "In use right now. It will go on the next launch.";
    }

    [RelayCommand]
    private async Task Install()
    {
        if (SelectedPack is not { } pack)
        {
            return;
        }

        IsBusy = true;
        StatusText = "Starting download...";
        gpuPackService.Progress += OnProgress;

        try
        {
            var message = await gpuPackService.InstallAsync(pack);
            Refresh();

            // Show the actual result, which the status line above only guessed at.
            StatusText = message;
        }
        finally
        {
            gpuPackService.Progress -= OnProgress;
            IsBusy = false;
        }
    }

    [RelayCommand]
    private void Cancel() => gpuPackService.Cancel();

    /// <summary>Points the install button at the selected pack, and returns the matching status line.</summary>
    private string? Refresh()
    {
        if (SelectedPack is not { } pack)
        {
            return null;
        }

        IsInstalled = pack.IsInstalled();
        ButtonText = $"Download {pack.SizeText}";

        return IsInstalled
            ? $"{pack.DisplayName} is installed."
            : $"{pack.DisplayName} needs a {pack.SizeText} download.";
    }

    private void OnProgress(long downloaded, long total)
    {
        // Format off the UI thread so the post carries only the finished string.
        var text = $"Downloading... {DownloadProgressText.Format(downloaded, total)}";
        Dispatcher.UIThread.Post(() => StatusText = text);
    }

    partial void OnSelectedBackendChanged(GpuBackend value)
    {
        if (isLoading)
        {
            return;
        }

        settingsService.Settings.Audio.GpuBackend = value;
        settingsService.SaveDebounced();
        StatusText = Refresh();
    }
}
