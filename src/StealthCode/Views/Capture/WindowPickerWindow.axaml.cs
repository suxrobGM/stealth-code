using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Threading;
using StealthCode.ScreenCapture.Services;
using StealthCode.ViewModels.Capture;

namespace StealthCode.Views.Capture;

// ReSharper disable once PartialTypeWithSinglePart
public partial class WindowPickerWindow : Window
{
    private readonly WindowPickerViewModel viewModel;

    public WindowPickerWindow(List<WindowInfo> windows)
    {
        viewModel = new WindowPickerViewModel(windows);
        DataContext = viewModel;

        InitializeComponent();

        viewModel.ResultTask.ContinueWith(_ =>
                Dispatcher.UIThread.Post(Close),
            TaskContinuationOptions.ExecuteSynchronously);
    }

    public Task<WindowInfo?> GetSelectionAsync()
    {
        return viewModel.ResultTask;
    }

    private void OnListDoubleTapped(object? sender, TappedEventArgs e)
    {
        viewModel.SelectCommand.Execute(null);
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            viewModel.CancelCommand.Execute(null);
            e.Handled = true;
        }

        base.OnKeyDown(e);
    }

    protected override void OnClosed(EventArgs e)
    {
        viewModel.OnClosed();
        base.OnClosed(e);
    }
}
