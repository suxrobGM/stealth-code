using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace StealthCode.ViewModels.Settings;

/// <summary>Hosts the settings panel sections. Each section owns its own slice of the saved settings.</summary>
public sealed partial class SettingsViewModel(
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

    /// <summary>One tab is visible at a time.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsGeneralTab))]
    [NotifyPropertyChangedFor(nameof(IsCaptureTab))]
    [NotifyPropertyChangedFor(nameof(IsAudioTab))]
    public partial SettingsTab SelectedTab { get; set; } = SettingsTab.General;

    public bool IsGeneralTab => SelectedTab == SettingsTab.General;
    public bool IsCaptureTab => SelectedTab == SettingsTab.Capture;
    public bool IsAudioTab => SelectedTab == SettingsTab.Audio;

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

    [RelayCommand]
    private void SelectTab(SettingsTab tab) => SelectedTab = tab;
}

public enum SettingsTab
{
    General,
    Capture,
    Audio
}
