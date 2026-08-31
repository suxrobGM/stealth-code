namespace StealthCode.ViewModels.Settings;

/// <summary>Hosts the settings panel sections. Each section owns its own slice of the saved settings.</summary>
public sealed class SettingsViewModel(
    GeneralSettingsViewModel general,
    CaptureSettingsViewModel capture,
    AudioSettingsViewModel audio,
    UpdateSettingsViewModel updates) : ViewModelBase
{
    // Updates has nothing saved to fill in, so it stays out of the load/unload fan-out.
    private readonly SettingsSectionViewModel[] sections = [general, capture, audio];

    public GeneralSettingsViewModel General { get; } = general;
    public CaptureSettingsViewModel Capture { get; } = capture;
    public AudioSettingsViewModel Audio { get; } = audio;
    public UpdateSettingsViewModel Updates { get; } = updates;

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
