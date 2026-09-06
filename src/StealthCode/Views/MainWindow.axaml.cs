using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Extensions.DependencyInjection;
using StealthCode.Messages;
using StealthCode.ScreenCapture.Services;
// ReSharper disable once RedundantUsingDirective
using StealthCode.Services;
using StealthCode.Utilities;
using StealthCode.ViewModels;
using StealthCode.Views;

namespace StealthCode;

public sealed partial class MainWindow : Window,
    IRecipient<SwitchTerminalMessage>,
    IRecipient<FallbackToShellMessage>,
    IRecipient<ApplyOpacityMessage>,
    IRecipient<NoFocusChangedMessage>,
    IRecipient<RequestRegionSelectionMessage>,
    IRecipient<RequestWindowSelectionMessage>,
    IRecipient<ShowToastMessage>
{
    private readonly MainWindowViewModel viewModel;

    public MainWindow()
    {
        viewModel = App.Services.GetRequiredService<MainWindowViewModel>();
        DataContext = viewModel;

        InitializeComponent();

        Opened += OnWindowOpened;
        Closing += OnWindowClosing;
    }

    public void Receive(ApplyOpacityMessage message)
    {
        WindowOpacityUtils.Apply(this, message.Opacity);
    }

    public void Receive(NoFocusChangedMessage message)
    {
        WindowNoFocusUtils.Apply(this, message.IsNoFocus);
    }

    /// <summary>Toasts can be raised from a thread pool thread, so the UI hop belongs here.</summary>
    public void Receive(ShowToastMessage message)
    {
        Dispatcher.UIThread.Post(() => Terminal.ShowToast(message.Text, message.Level));
    }

    public void Receive(FallbackToShellMessage message)
    {
        Terminal.StartProcess("cmd.exe", [], Environment.CurrentDirectory);
    }

    public async void Receive(RequestRegionSelectionMessage message)
    {
        var overlay = new RegionSelectionWindow();
        overlay.Show();
        var result = await overlay.GetSelectionAsync();
        if (result.HasValue)
        {
            var r = result.Value;
            WeakReferenceMessenger.Default.Send(new RegionSelectedMessage(r.X, r.Y, r.Width, r.Height));
        }
    }

    public async void Receive(RequestWindowSelectionMessage message)
    {
        var ownHandle = TryGetPlatformHandle()?.Handle ?? IntPtr.Zero;
        var windows = WindowEnumerationService.GetVisibleWindows(ownHandle);
        var picker = new WindowPickerWindow(windows);
        await picker.ShowDialog(this);
        var result = await picker.GetSelectionAsync();
        if (result is not null)
        {
            WeakReferenceMessenger.Default.Send(new WindowSelectedMessage(result.Handle, result.Title));
        }
    }

    public void Receive(SwitchTerminalMessage message)
    {
        viewModel.PtyService.Stop();
        Terminal.Reset();
        Terminal.StartProcess(message.Provider.Command, message.Provider.Args, Environment.CurrentDirectory);
    }

    private void OnWindowOpened(object? sender, EventArgs e)
    {
#if DEBUG
        // Protection is off in DEBUG; the badge says so.
        viewModel.IsProtected = false;
#else
        viewModel.IsProtected = ContentProtectionService.EnableProtection(this);
#endif

        WeakReferenceMessenger.Default.Register<SwitchTerminalMessage>(this);
        WeakReferenceMessenger.Default.Register<FallbackToShellMessage>(this);
        WeakReferenceMessenger.Default.Register<ApplyOpacityMessage>(this);
        WeakReferenceMessenger.Default.Register<NoFocusChangedMessage>(this);
        WeakReferenceMessenger.Default.Register<RequestRegionSelectionMessage>(this);
        WeakReferenceMessenger.Default.Register<RequestWindowSelectionMessage>(this);
        WeakReferenceMessenger.Default.Register<ShowToastMessage>(this);

        Terminal.Initialize(viewModel.PtyService);

        var provider = viewModel.ActiveProvider;
        Terminal.StartProcess(provider.Command, provider.Args, Environment.CurrentDirectory);

        var hwnd = TryGetPlatformHandle()?.Handle ?? IntPtr.Zero;
        viewModel.Initialize(hwnd);
    }

    private void OnMinimizeClick(object? sender, RoutedEventArgs e)
    {
        WindowState = WindowState.Minimized;
    }

    private void OnMaximizeClick(object? sender, RoutedEventArgs e)
    {
        WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
    }

    private void OnCloseClick(object? sender, RoutedEventArgs e)
    {
        Close();
    }

    private void OnWindowClosing(object? sender, WindowClosingEventArgs e)
    {
        WeakReferenceMessenger.Default.UnregisterAll(this);
        viewModel.Cleanup();
        Terminal.Dispose();
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);

        var pos = e.GetPosition(this);
        if (pos.Y <= 28 && e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            // Don't drag if clicking on an interactive control
            var source = e.Source as Visual;
            while (source is not null && source != this)
            {
                if (source is Button or ComboBox)
                {
                    return;
                }

                source = source.GetVisualParent();
            }

            BeginMoveDrag(e);
        }
    }
}
