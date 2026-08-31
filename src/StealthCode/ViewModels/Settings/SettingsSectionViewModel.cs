using CommunityToolkit.Mvvm.Messaging;
using StealthCode.Messages;
using StealthCode.Services;

namespace StealthCode.ViewModels.Settings;

/// <summary>One section of the settings panel, backed by its own slice of the saved settings.</summary>
public abstract class SettingsSectionViewModel(SettingsService settingsService) : ViewModelBase
{
    protected SettingsService SettingsService { get; } = settingsService;

    /// <summary>True while <see cref="Load"/> fills the section, so change handlers stay quiet.</summary>
    protected bool IsLoading { get; private set; }

    /// <summary>Fills the section from the saved settings and starts listening for messages.</summary>
    public void Load()
    {
        IsLoading = true;

        try
        {
            LoadCore();
        }
        finally
        {
            IsLoading = false;
        }
    }

    /// <summary>Stops listening. Only sections that register for messages need to override this.</summary>
    public virtual void Unload()
    {
    }

    protected abstract void LoadCore();

    /// <summary>Queues a save. Does nothing while loading, so filling the form is not written back.</summary>
    protected void Save()
    {
        if (IsLoading)
        {
            return;
        }

        SettingsService.SaveDebounced();
    }

    /// <summary>Saves a changed hotkey and asks the window to re-register it.</summary>
    protected void SaveHotkey(string name, string hotkey)
    {
        if (IsLoading)
        {
            return;
        }

        WeakReferenceMessenger.Default.Send(new HotkeyChangedMessage(name, hotkey));
        SettingsService.SaveDebounced();
    }
}
