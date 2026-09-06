using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using StealthCode.ScreenCapture.Services;

namespace StealthCode.ViewModels.Capture;

// ReSharper disable once PartialTypeWithSinglePart
public sealed partial class WindowPickerViewModel(List<WindowInfo> windows) : ViewModelBase
{
    private readonly TaskCompletionSource<WindowInfo?> tcs = new();

    [ObservableProperty]
    public partial IReadOnlyList<string> WindowTitles { get; set; } = windows.Select(w => w.Title).ToList();

    [ObservableProperty]
    public partial int SelectedIndex { get; set; } = -1;

    public Task<WindowInfo?> ResultTask => tcs.Task;

    [RelayCommand]
    private void Select()
    {
        if (SelectedIndex >= 0 && SelectedIndex < windows.Count)
        {
            tcs.TrySetResult(windows[SelectedIndex]);
        }
    }

    [RelayCommand]
    private void Cancel()
    {
        tcs.TrySetResult(null);
    }

    public void OnClosed()
    {
        tcs.TrySetResult(null);
    }
}
