using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Microsoft.Extensions.DependencyInjection;
using StealthCode.Audio;
using StealthCode.ScreenCapture;
using StealthCode.Services;
using StealthCode.Services.Hotkeys;
using StealthCode.Services.Injection;
using StealthCode.Terminal;
using StealthCode.Terminal.Pty;
using StealthCode.Updater;
using StealthCode.ViewModels.Capture;
using StealthCode.ViewModels.Settings;
using StealthCode.ViewModels.Shell;
using StealthCode.Views.Shell;

namespace StealthCode;

public partial class App : Application
{
    public static IServiceProvider Services { get; private set; } = null!;

    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    public override void OnFrameworkInitializationCompleted()
    {
        var services = new ServiceCollection();
        ConfigureServices(services);
        Services = services.BuildServiceProvider();

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.MainWindow = new MainWindow();
        }

        base.OnFrameworkInitializationCompleted();
    }

    private void OnTrayShow(object? sender, EventArgs e)
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var window = desktop.MainWindow;
            if (window is null)
            {
                return;
            }

            window.Show();
            window.WindowState = WindowState.Normal;
            window.Activate();
        }
    }

    private void OnTrayExit(object? sender, EventArgs e)
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.Shutdown();
        }
    }

    private static void ConfigureServices(IServiceCollection services)
    {
        // Modules
        services.AddTerminal();
        services.AddScreenCapture();
        services.AddAudioCapture();

        // App services
        services.AddSingleton<SettingsService>();
        services.AddSingleton<CliProviderRegistry>();
        services.AddSingleton<HotkeyService>();
        services.AddSingleton<GlobalHotkeys>();
        services.AddSingleton<CaptureInjectorService>();
        services.AddSingleton<AudioInjectorService>();
        services.AddSingleton<WhisperModelInstaller>();
        services.AddUpdater();

        // ViewModels
        services.AddSingleton<AudioViewModel>();
        services.AddSingleton<StatusBarViewModel>();
        services.AddSingleton<MainWindowViewModel>();

        // Settings panel
        services.AddSingleton<GeneralSettingsViewModel>();
        services.AddSingleton<CaptureSettingsViewModel>();
        services.AddSingleton<GpuPackSettingsViewModel>();
        services.AddSingleton<AudioSettingsViewModel>();
        services.AddSingleton<UpdateSettingsViewModel>();
        services.AddSingleton<SettingsViewModel>();
    }
}
