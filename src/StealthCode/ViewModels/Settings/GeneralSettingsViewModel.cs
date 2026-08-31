using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Messaging;
using StealthCode.Messages;
using StealthCode.ScreenCapture.Models;
using StealthCode.Services;

namespace StealthCode.ViewModels.Settings;

/// <summary>CLI provider, window opacity, and the hotkeys that belong to no single module.</summary>
public sealed partial class GeneralSettingsViewModel(
    SettingsService settingsService,
    CliProviderRegistry providerRegistry) : SettingsSectionViewModel(settingsService)
{
    [ObservableProperty]
    public partial IReadOnlyList<string> ProviderNames { get; set; } = [];

    [ObservableProperty]
    public partial int SelectedProviderIndex { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(OpacityValueText))]
    public partial double Opacity { get; set; } = 1.0;

    [ObservableProperty]
    public partial string OpacityHotkey { get; set; } = "Ctrl+Shift+O";

    [ObservableProperty]
    public partial string NoFocusHotkey { get; set; } = "Ctrl+Shift+F";

    /// <summary>Opacity as a percentage, shown beside the slider.</summary>
    public string OpacityValueText => $"{(int)(Opacity * 100)}%";

    protected override void LoadCore()
    {
        var settings = SettingsService.Settings;
        var providers = providerRegistry.GetAllProviders();

        ProviderNames = [.. providers.Select(p => p.Name)];
        SelectedProviderIndex = IndexOfProvider(providers, settings.ActiveProviderId);

        Opacity = settings.WindowOpacity;
        OpacityHotkey = settings.OpacityHotkey;
        NoFocusHotkey = settings.NoFocusHotkey;
    }

    private static int IndexOfProvider(IReadOnlyList<CliProviderConfig> providers, string id)
    {
        for (var i = 0; i < providers.Count; i++)
        {
            if (providers[i].Id == id)
            {
                return i;
            }
        }

        return 0;
    }

    partial void OnSelectedProviderIndexChanged(int value)
    {
        if (IsLoading)
        {
            return;
        }

        WeakReferenceMessenger.Default.Send(new SettingsProviderChangedMessage(value));
    }

    partial void OnOpacityChanged(double value)
    {
        if (IsLoading)
        {
            return;
        }

        SettingsService.Settings.WindowOpacity = value;
        WeakReferenceMessenger.Default.Send(new OpacityChangedMessage(value));
        Save();
    }

    partial void OnOpacityHotkeyChanged(string value)
    {
        SettingsService.Settings.OpacityHotkey = value;
        SaveHotkey("opacity", value);
    }

    partial void OnNoFocusHotkeyChanged(string value)
    {
        SettingsService.Settings.NoFocusHotkey = value;
        SaveHotkey("nofocus", value);
    }
}
