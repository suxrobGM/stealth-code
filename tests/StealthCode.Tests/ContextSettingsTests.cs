using System.Text;
using StealthCode.Models;
using StealthCode.Services;
using StealthCode.Services.Injection;
using StealthCode.ViewModels.Settings;

namespace StealthCode.Tests;

public sealed class ContextSettingsTests : IDisposable
{
    private readonly string directory = Path.Combine(Path.GetTempPath(), "StealthCode.Tests", Guid.NewGuid().ToString("N"));
    private readonly SettingsService settings;
    private readonly ContextSettingsViewModel viewModel;

    public ContextSettingsTests()
    {
        Directory.CreateDirectory(directory);
        settings = new SettingsService(Path.Combine(directory, "settings.json"));
        viewModel = new ContextSettingsViewModel(settings);
        viewModel.Load();
    }

    [Fact]
    public void OlderSettingsLoadEmptyDisabledContext()
    {
        File.WriteAllText(Path.Combine(directory, "old.json"), "{\"windowOpacity\":0.6,\"audio\":{\"language\":\"de\"}}");
        var old = new SettingsService(Path.Combine(directory, "old.json")).Settings;
        Assert.False(old.Context.Enabled);
        Assert.Empty(old.Context.Background);
        Assert.Equal(0.6, old.WindowOpacity);
        Assert.Equal("de", old.Audio.Language);
    }

    [Fact]
    public void NullContextFieldsAreNormalizedWithoutLosingOtherSettings()
    {
        File.WriteAllText(Path.Combine(directory, "null.json"), "{\"windowOpacity\":0.4,\"context\":{\"background\":null}}");
        var loaded = new SettingsService(Path.Combine(directory, "null.json")).Settings;
        Assert.Empty(loaded.Context.Background);
        Assert.Equal(0.4, loaded.WindowOpacity);
    }

    [Fact]
    public void DraftsDoNotReachRequestsUntilSavedAndEnabled()
    {
        viewModel.Background = "Reviewed candidate";
        viewModel.ExperienceStories = "I migrated an API.";
        viewModel.Opportunity = "Acme needs Go.";
        Assert.True(viewModel.HasUnsavedChanges);
        Assert.Empty(settings.Settings.Context.Background);
        Assert.DoesNotContain("Reviewed candidate", CandidateContextPromptBuilder.ForAudio("Answer", "Who are you?", settings.Settings.Context));

        viewModel.SaveProfileCommand.Execute(null);
        Assert.False(settings.Settings.Context.Enabled);
        Assert.False(viewModel.HasUnsavedChanges);
        viewModel.UseSavedContext = true;
        Assert.Contains("Reviewed candidate", CandidateContextPromptBuilder.ForAudio("Answer", "Who are you?", settings.Settings.Context));

        var snapshot = settings.Settings.Context;
        viewModel.Background = "Unsaved replacement";
        viewModel.UseSavedContext = false;
        viewModel.UseSavedContext = true;
        Assert.Equal("Reviewed candidate", settings.Settings.Context.Background);
        viewModel.SaveProfileCommand.Execute(null);
        Assert.Contains("Unsaved replacement", CandidateContextPromptBuilder.ForAudio("Answer", "Who are you?", settings.Settings.Context));
        Assert.Equal("Reviewed candidate", snapshot.Background);

        var reloaded = new SettingsService(Path.Combine(directory, "settings.json")).Settings;
        Assert.Equal(settings.Settings.Context, reloaded.Context);
        Assert.DoesNotContain("characterCount", File.ReadAllText(Path.Combine(directory, "settings.json")));
    }

    [Fact]
    public void ClosingDiscardsDraftAndPreview()
    {
        viewModel.Background = "Saved";
        viewModel.SaveProfileCommand.Execute(null);
        viewModel.Background = "Discard me";
        viewModel.ImportPreview = "Also discard";
        viewModel.HasImportPreview = true;
        viewModel.Unload();
        viewModel.Load();
        Assert.Equal("Saved", viewModel.Background);
        Assert.False(viewModel.HasImportPreview);
        Assert.False(viewModel.HasUnsavedChanges);
    }

    [Fact]
    public async Task ImportIsReviewedBeforeReplacingDraftOrSaving()
    {
        viewModel.Background = "Manual draft";
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes("Imported résumé"));
        await viewModel.ImportAsync("resume.txt", stream);
        Assert.True(viewModel.HasImportPreview);
        Assert.Equal("Manual draft", viewModel.Background);
        Assert.Empty(settings.Settings.Context.Background);
        viewModel.ImportPreview += " reviewed";
        viewModel.ReplaceBackgroundCommand.Execute(null);
        Assert.Equal("Imported résumé reviewed", viewModel.Background);
        Assert.False(viewModel.HasImportPreview);
        Assert.Empty(settings.Settings.Context.Background);
        Assert.False(settings.Settings.Context.Enabled);
    }

    [Fact]
    public async Task FailedAndCancelledImportsPreserveDraftAndSavedProfile()
    {
        viewModel.Background = "Saved";
        viewModel.SaveProfileCommand.Execute(null);
        viewModel.Background = "Manual draft";
        using var invalid = new MemoryStream();
        await viewModel.ImportAsync("broken.pdf", invalid);
        Assert.NotEmpty(viewModel.Error);
        Assert.Equal("Manual draft", viewModel.Background);
        Assert.Equal("Saved", settings.Settings.Context.Background);

        using var valid = new MemoryStream(Encoding.UTF8.GetBytes("Replacement"));
        await viewModel.ImportAsync("resume.txt", valid);
        viewModel.CancelImportCommand.Execute(null);
        Assert.False(viewModel.HasImportPreview);
        Assert.Equal("Manual draft", viewModel.Background);
    }

    [Fact]
    public void RejectsOversizedProfileWithoutTruncation()
    {
        viewModel.Background = new string('a', CandidateContextSettings.MaxCharacters);
        viewModel.SaveProfileCommand.Execute(null);
        Assert.Empty(viewModel.Error);
        viewModel.ExperienceStories = "x";
        viewModel.SaveProfileCommand.Execute(null);
        Assert.Contains("32,000", viewModel.Error);
        Assert.Equal(CandidateContextSettings.MaxCharacters + 1, viewModel.CharacterCount);
        Assert.Empty(settings.Settings.Context.ExperienceStories);
        Assert.True(viewModel.HasUnsavedChanges);
    }

    [Fact]
    public void FailedSavePreservesActiveProfileAndDraftAndRollsBackToggle()
    {
        viewModel.Background = "Saved";
        viewModel.SaveProfileCommand.Execute(null);
        using var locked = File.Open(Path.Combine(directory, "settings.json"), FileMode.Open, FileAccess.Read, FileShare.Read);
        viewModel.Background = "Keep draft for retry";
        viewModel.SaveProfileCommand.Execute(null);
        Assert.NotEmpty(viewModel.Error);
        Assert.Equal("Saved", settings.Settings.Context.Background);
        Assert.Equal("Keep draft for retry", viewModel.Background);
        Assert.True(viewModel.HasUnsavedChanges);
        viewModel.UseSavedContext = true;
        Assert.False(viewModel.UseSavedContext);
        Assert.False(settings.Settings.Context.Enabled);
        Assert.Contains("Saved", File.ReadAllText(Path.Combine(directory, "settings.json")));
    }

    [Fact]
    public async Task DebouncedGeneralSettingsDoNotOverwriteNewProfile()
    {
        settings.Settings.WindowOpacity = 0.6;
        settings.SaveDebounced();
        viewModel.Background = "Latest profile";
        viewModel.SaveProfileCommand.Execute(null);
        viewModel.UseSavedContext = true;
        await Task.Delay(900, TestContext.Current.CancellationToken);
        var loaded = new SettingsService(Path.Combine(directory, "settings.json")).Settings;
        Assert.Equal("Latest profile", loaded.Context.Background);
        Assert.True(loaded.Context.Enabled);
        Assert.Equal(0.6, loaded.WindowOpacity);
    }

    public void Dispose()
    {
        viewModel.Unload();
        Directory.Delete(directory, true);
    }
}
