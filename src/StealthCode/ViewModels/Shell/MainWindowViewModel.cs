using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using StealthCode.Messages;
using StealthCode.Models;
using StealthCode.ScreenCapture.Models;
using StealthCode.ScreenCapture.Utilities;
using StealthCode.Services;
using StealthCode.Services.Hotkeys;
using StealthCode.Services.Injection;
using StealthCode.Terminal.Pty;
using StealthCode.Updater.Services;
using StealthCode.Utilities;
using StealthCode.ViewModels.Settings;

namespace StealthCode.ViewModels.Shell;

public sealed partial class MainWindowViewModel : ViewModelBase,
    IRecipient<OpacityChangedMessage>,
    IRecipient<HotkeyChangedMessage>,
    IRecipient<UpdateAvailableMessage>
{
    private readonly CaptureInjectorService captureInjectorService;
    private readonly GlobalHotkeys hotkeys;
    private readonly CliProviderRegistry providerRegistry;
    private readonly SettingsService settingsService;
    private readonly SettingsViewModel settingsViewModel;
    private readonly UpdateService updateService;

    private bool initialized;
    private CliProviderConfig? activeProvider;

    public MainWindowViewModel(
        PtyService ptyService,
        SettingsService settingsService,
        CliProviderRegistry providerRegistry,
        SettingsViewModel settingsViewModel,
        GlobalHotkeys hotkeys,
        CaptureInjectorService captureInjectorService,
        AudioViewModel audioViewModel,
        StatusBarViewModel statusBarViewModel,
        UpdateService updateService)
    {
        this.settingsService = settingsService;
        this.providerRegistry = providerRegistry;
        this.settingsViewModel = settingsViewModel;
        this.hotkeys = hotkeys;
        this.captureInjectorService = captureInjectorService;
        this.updateService = updateService;
        PtyService = ptyService;
        Audio = audioViewModel;
        Status = statusBarViewModel;

        LoadFromSettings();

        WeakReferenceMessenger.Default.Register<OpacityChangedMessage>(this);
        WeakReferenceMessenger.Default.Register<HotkeyChangedMessage>(this);
        WeakReferenceMessenger.Default.Register<UpdateAvailableMessage>(this);
    }

    [ObservableProperty]
    public partial IReadOnlyList<string> ProviderNames { get; set; } = [];

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(CaptureCommand))]
    [NotifyPropertyChangedFor(nameof(ActiveProvider))]
    [NotifyPropertyChangedFor(nameof(CanCapture))]
    [NotifyPropertyChangedFor(nameof(CaptureTip))]
    public partial int SelectedProviderIndex { get; set; }

    [ObservableProperty]
    public partial bool IsAlwaysOnTop { get; set; }

    [ObservableProperty]
    public partial double WindowOpacity { get; set; } = 1.0;

    [ObservableProperty]
    public partial bool IsSettingsVisible { get; set; }

    [ObservableProperty]
    public partial ViewModelBase? SettingsContent { get; set; }

    /// <summary>Raw hotkeys; the bar renders them into tooltips.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CaptureTip))]
    public partial string CaptureHotkey { get; set; } = "Ctrl+Shift+C";

    [ObservableProperty]
    public partial string MultiCaptureHotkey { get; set; } = "Ctrl+Shift+X";

    [ObservableProperty]
    public partial string OpacityHotkey { get; set; } = "Ctrl+Shift+O";

    [ObservableProperty]
    public partial string NoFocusHotkey { get; set; } = "Ctrl+Shift+F";

    [ObservableProperty]
    public partial bool IsNoFocus { get; set; }

    /// <summary>Whether SetWindowDisplayAffinity actually took.</summary>
    [ObservableProperty]
    public partial bool IsProtected { get; set; }

    [ObservableProperty]
    public partial bool IsUpdateAvailable { get; set; }

    public PtyService PtyService { get; }
    public AudioViewModel Audio { get; }
    public StatusBarViewModel Status { get; }

    /// <summary>False for CLIs that cannot read an image.</summary>
    public bool CanCapture => ActiveProvider.SupportsImageInput;

    /// <summary>Hover text. An unsupported CLI explains itself here instead of just dimming.</summary>
    public string CaptureTip => CanCapture
        ? $"Capture and send  ({CaptureHotkey})"
        : $"{ActiveProvider.Name} cannot accept images";

    /// <summary>Read from the index, not settings: command state updates before settings are written.</summary>
    public CliProviderConfig ActiveProvider => activeProvider ??= ResolveActiveProvider();

    public void Receive(HotkeyChangedMessage message)
    {
        switch (message.Name)
        {
            case "capture":
                CaptureHotkey = message.Hotkey;
                break;
            case "multicapture":
                MultiCaptureHotkey = message.Hotkey;
                break;
            case "opacity":
                OpacityHotkey = message.Hotkey;
                break;
            case "nofocus":
                NoFocusHotkey = message.Hotkey;
                break;
            case "audio":
                Audio.Hotkey = message.Hotkey;
                break;
        }

        hotkeys.Rebind(message.Name, message.Hotkey);
    }

    public void Receive(OpacityChangedMessage message) => WindowOpacity = message.Opacity;

    public void Receive(UpdateAvailableMessage message) => IsUpdateAvailable = message.Available;

    public void Initialize(IntPtr windowHandle)
    {
        initialized = true;
        PtyService.ProcessExited += OnProcessExited;
        CleanupUtils.CleanupOldCaptures();
        WeakReferenceMessenger.Default.Send(new ApplyOpacityMessage(WindowOpacity));
        Audio.Initialize();

        hotkeys.Bind(windowHandle, new Dictionary<string, Action>
        {
            ["capture"] = RunCapture,
            ["multicapture"] = () => MultiCaptureCommand.Execute(null),
            ["audio"] = Audio.Toggle,
            ["opacity"] = () => CycleOpacityCommand.Execute(null),
            ["nofocus"] = () => ToggleNoFocusCommand.Execute(null)
        });

        _ = CheckForUpdateOnStartupAsync();
    }

    public void Cleanup()
    {
        Audio.Cleanup();
        hotkeys.Dispose();
        WeakReferenceMessenger.Default.UnregisterAll(this);
        PtyService.Stop();
        PtyService.Dispose();
    }

    [RelayCommand(CanExecute = nameof(CanCapture))]
    private async Task Capture()
    {
        await captureInjectorService.CaptureAndInjectAsync();

        // Multi-capture may have just been finalized, which empties the queue.
        Status.PendingCaptureCount = captureInjectorService.PendingCount;
    }

    [RelayCommand]
    private async Task MultiCapture()
    {
        await captureInjectorService.MultiCaptureAsync();
        Status.PendingCaptureCount = captureInjectorService.PendingCount;
    }

    [RelayCommand]
    private void ToggleNoFocus()
    {
        IsNoFocus = !IsNoFocus;
        WeakReferenceMessenger.Default.Send(new NoFocusChangedMessage(IsNoFocus));
        WeakReferenceMessenger.Default.Send(new ShowToastMessage(IsNoFocus ? "No-focus on" : "No-focus off"));
    }

    [RelayCommand]
    private void TogglePin() => IsAlwaysOnTop = !IsAlwaysOnTop;

    [RelayCommand]
    private void ToggleSettings()
    {
        if (IsSettingsVisible)
        {
            settingsViewModel.Unload();
            IsSettingsVisible = false;
            return;
        }

        settingsViewModel.Load();
        SettingsContent = settingsViewModel;
        IsSettingsVisible = true;
    }

    /// <summary>Steps down through the presets, wrapping back to fully opaque.</summary>
    [RelayCommand]
    private void CycleOpacity()
    {
        var presets = GeneralSettingsViewModel.OpacityPresets;
        WindowOpacity = presets.FirstOrDefault(p => WindowOpacity > p + 0.01, presets[0]);
    }

    /// <summary>Hotkey path. A disabled command executes silently, so an unsupported CLI says so instead.</summary>
    private void RunCapture()
    {
        if (CanCapture)
        {
            CaptureCommand.Execute(null);
            return;
        }

        WeakReferenceMessenger.Default.Send(new ShowToastMessage(CaptureTip, StatusLevel.Warning));
    }

    private async Task CheckForUpdateOnStartupAsync()
    {
        try
        {
            if (await updateService.CheckForUpdateAsync() is not null)
            {
                IsUpdateAvailable = true;
                WeakReferenceMessenger.Default.Send(new UpdateAvailableMessage(true));
            }
        }
        catch
        {
            // Silently ignore startup update check failures
        }
    }

    partial void OnSelectedProviderIndexChanged(int value)
    {
        activeProvider = ResolveActiveProvider();

        var providers = providerRegistry.GetAllProviders();
        if (value < 0 || value >= providers.Count)
        {
            return;
        }

        var provider = providers[value];
        settingsService.Settings.ActiveProviderId = provider.Id;
        settingsService.Save();

        if (initialized)
        {
            WeakReferenceMessenger.Default.Send(new SwitchTerminalMessage(provider));
        }
    }

    partial void OnIsAlwaysOnTopChanged(bool value)
    {
        settingsService.Settings.AlwaysOnTop = value;
        settingsService.Save();
    }

    partial void OnWindowOpacityChanged(double value)
    {
        settingsService.Settings.WindowOpacity = value;

        if (initialized)
        {
            WeakReferenceMessenger.Default.Send(new ShowToastMessage($"Opacity {(int)(value * 100)}%"));
            WeakReferenceMessenger.Default.Send(new ApplyOpacityMessage(value));
        }
    }

    /// <summary>
    /// Only reached for the terminal that is currently running: <see cref="PtyService"/> ignores exits from
    /// processes it has already replaced, so a CLI switch cannot be mistaken for the new terminal dying.
    /// </summary>
    private void OnProcessExited(int _)
    {
        PtyService.Stop();
        WeakReferenceMessenger.Default.Send(new FallbackToShellMessage());
    }

    private void LoadFromSettings()
    {
        var settings = settingsService.Settings;
        var providers = providerRegistry.GetAllProviders();
        var activeId = providerRegistry.GetActiveProvider().Id;

        ProviderNames = [.. providers.Select(p => p.Name)];
        SelectedProviderIndex = Math.Max(0, providers.ToList().FindIndex(p => p.Id == activeId));

        IsAlwaysOnTop = settings.AlwaysOnTop;
        WindowOpacity = settings.WindowOpacity;
        CaptureHotkey = settings.Capture.Hotkey;
        MultiCaptureHotkey = settings.Capture.MultiCaptureHotkey;
        OpacityHotkey = settings.OpacityHotkey;
        NoFocusHotkey = settings.NoFocusHotkey;
        Audio.LoadFromSettings();

        activeProvider = ResolveActiveProvider();
    }

    private CliProviderConfig ResolveActiveProvider()
    {
        var providers = providerRegistry.GetAllProviders();
        return SelectedProviderIndex >= 0 && SelectedProviderIndex < providers.Count
            ? providers[SelectedProviderIndex]
            : providerRegistry.GetActiveProvider();
    }
}
