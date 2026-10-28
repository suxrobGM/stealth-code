using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using StealthCode.Models;
using StealthCode.Services;

namespace StealthCode.ViewModels.Settings;

/// <summary>Edits a draft; only Save changes the profile used by requests.</summary>
public sealed partial class ContextSettingsViewModel(SettingsService settingsService)
    : SettingsSectionViewModel(settingsService)
{
    private CancellationTokenSource? importCancellation;
    public long SessionVersion { get; private set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CharacterCount), nameof(CharacterCountText), nameof(HasUnsavedChanges))]
    public partial string Background { get; set; } = "";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CharacterCount), nameof(CharacterCountText), nameof(HasUnsavedChanges))]
    public partial string ExperienceStories { get; set; } = "";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CharacterCount), nameof(CharacterCountText), nameof(HasUnsavedChanges))]
    public partial string Opportunity { get; set; } = "";

    [ObservableProperty]
    public partial bool UseSavedContext { get; set; }

    [ObservableProperty]
    public partial string Error { get; set; } = "";

    [ObservableProperty]
    public partial string SaveStatus { get; set; } = "";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PreviewCharacterCountText))]
    public partial string ImportPreview { get; set; } = "";

    [ObservableProperty]
    public partial bool HasImportPreview { get; set; }

    [ObservableProperty]
    public partial bool IsImporting { get; set; }

    public int CharacterCount => Draft.CharacterCount;
    public string CharacterCountText => $"{CharacterCount:N0} / {CandidateContextSettings.MaxCharacters:N0} characters";
    public string PreviewCharacterCountText => $"{ImportPreview.Length:N0} characters imported. Edit before replacing.";
    public bool HasUnsavedChanges => Draft != SettingsService.Settings.Context;

    /// <summary>The edited text with the saved Enabled flag, so it compares equal to an unchanged profile.</summary>
    private CandidateContextSettings Draft => SettingsService.Settings.Context with
    {
        Background = Background,
        ExperienceStories = ExperienceStories,
        Opportunity = Opportunity
    };

    protected override void LoadCore()
    {
        SessionVersion++;
        ResetChanges();
        UseSavedContext = SettingsService.Settings.Context.Enabled;
    }

    public override void Unload()
    {
        SessionVersion++;
        ResetChanges();
    }

    /// <summary>The view owns the native file picker; extraction remains independently testable.</summary>
    public async Task ImportAsync(string filename, Stream stream)
    {
        if (IsImporting)
        {
            return;
        }

        Error = "";
        HasImportPreview = false;
        ImportPreview = "";
        var cancellation = new CancellationTokenSource();
        importCancellation = cancellation;
        IsImporting = true;
        try
        {
            var text = await Task.Run(() => ResumeTextExtractor.Extract(filename, stream, cancellation.Token));
            if (!cancellation.IsCancellationRequested)
            {
                ImportPreview = text;
                HasImportPreview = true;
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException)
        {
            if (!cancellation.IsCancellationRequested)
            {
                Error = ex is InvalidDataException ? ex.Message : "Could not open this resume. Close other apps using it, then retry or paste the text.";
            }
        }
        finally
        {
            if (ReferenceEquals(importCancellation, cancellation))
            {
                importCancellation = null;
                IsImporting = false;
            }

            cancellation.Dispose();
        }
    }

    [RelayCommand]
    private void ReplaceBackground()
    {
        Background = ImportPreview;
        CancelImport();
    }

    [RelayCommand]
    private void CancelImport()
    {
        importCancellation?.Cancel();
        ImportPreview = "";
        HasImportPreview = false;
    }

    [RelayCommand]
    private void ResetChanges()
    {
        var saved = SettingsService.Settings.Context;
        Background = saved.Background;
        ExperienceStories = saved.ExperienceStories;
        Opportunity = saved.Opportunity;
        CancelImport();
        Error = "";
        SaveStatus = "";
    }

    [RelayCommand]
    private void SaveProfile()
    {
        Error = "";
        SaveStatus = "";
        if (CharacterCount > CandidateContextSettings.MaxCharacters)
        {
            Error = $"Keep background, stories, and opportunity within {CandidateContextSettings.MaxCharacters:N0} characters combined. Nothing was saved.";
            return;
        }

        var profile = Draft;
        if (TrySave(profile))
        {
            SaveStatus = profile.Enabled ? "Saved. Context will accompany subsequent requests." : "Saved. Enable Use saved context to send it.";
            OnPropertyChanged(nameof(HasUnsavedChanges));
        }
    }

    partial void OnUseSavedContextChanged(bool value)
    {
        if (IsLoading || value == SettingsService.Settings.Context.Enabled)
        {
            return;
        }

        Error = "";
        SaveStatus = "";
        if (!TrySave(SettingsService.Settings.Context with { Enabled = value }))
        {
            UseSavedContext = SettingsService.Settings.Context.Enabled;
        }
    }

    private bool TrySave(CandidateContextSettings profile)
    {
        try
        {
            SettingsService.SaveContext(profile);
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException)
        {
            Error = "Could not save context. The previous profile remains active; your draft is available to retry.";
            return false;
        }
    }

    partial void OnBackgroundChanged(string value) => SaveStatus = "";
    partial void OnExperienceStoriesChanged(string value) => SaveStatus = "";
    partial void OnOpportunityChanged(string value) => SaveStatus = "";
}
