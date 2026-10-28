using CommunityToolkit.Mvvm.ComponentModel;

namespace StealthCode.ViewModels.Settings;

/// <summary>Hosts the settings panel sections. Each section owns its own slice of the saved settings.</summary>
public sealed partial class SettingsViewModel(
    GeneralSettingsViewModel general,
    CaptureSettingsViewModel capture,
    AudioSettingsViewModel audio,
    ContextSettingsViewModel context,
    UpdateSettingsViewModel updates) : ViewModelBase
{
    // Updates has nothing saved to fill in, so it stays out of the load/unload fan-out.
    private readonly SettingsSectionViewModel[] sections = [general, capture, audio, context];

    public GeneralSettingsViewModel General { get; } = general;
    public CaptureSettingsViewModel Capture { get; } = capture;
    public AudioSettingsViewModel Audio { get; } = audio;
    public ContextSettingsViewModel Context { get; } = context;
    public UpdateSettingsViewModel Updates { get; } = updates;

    /// <summary>Kept here so the open tab survives closing and reopening the panel.</summary>
    [ObservableProperty]
    public partial int SelectedTabIndex { get; set; }

    /// <summary>Fills every section from the saved settings and starts listening for messages.</summary>
    public void Load()
    {
        foreach (var section in sections)
        {
            section.Load();
        }
    }

    /// <summary>Stops listening for the dialog results that only the open panel can ask for.</summary>
    public void Unload()
    {
        foreach (var section in sections)
        {
            section.Unload();
        }
    }
}
