using StealthCode.ScreenCapture.Models;

namespace StealthCode.Services;

/// <summary>
/// Manages the registry of CLI providers, including built-in and user-defined ones.
/// </summary>
public sealed class CliProviderRegistry(SettingsService settingsService)
{
    private static readonly List<CliProviderConfig> BuiltInProviders =
    [
        new()
        {
            Id = "claude-code",
            Name = "Claude Code",
            Command = "claude",
            Args = [],
            SupportsImageInput = true,
            ImageMode = ImageInputMode.FilePath,
            DefaultSystemPrompt = "Analyze the following screenshot. Describe what you see and suggest actions or code changes based on the content."
        },
        new()
        {
            Id = "codex",
            Name = "Codex",
            Command = "codex",
            Args = [],
            SupportsImageInput = true,
            ImageMode = ImageInputMode.FilePath,
            DefaultSystemPrompt = "Analyze the following screenshot and describe what you see."
        },
        new()
        {
            Id = "gemini-cli",
            Name = "Gemini CLI",
            Command = "gemini",
            Args = [],
            SupportsImageInput = true,
            ImageMode = ImageInputMode.FilePath,
            DefaultSystemPrompt = "Analyze the following screenshot and describe what you see."
        }
    ];

    public IReadOnlyList<CliProviderConfig> GetAllProviders()
    {
        return [.. BuiltInProviders, .. settingsService.Settings.CustomProviders];
    }

    public CliProviderConfig? GetProvider(string id) => GetAllProviders().FirstOrDefault(p => p.Id == id);

    public CliProviderConfig GetActiveProvider()
    {
        return GetProvider(settingsService.Settings.ActiveProviderId) ?? BuiltInProviders[0];
    }

    public void AddCustomProvider(CliProviderConfig provider)
    {
        settingsService.Settings.CustomProviders.Add(provider);
        settingsService.Save();
    }

    public void RemoveCustomProvider(string id)
    {
        settingsService.Settings.CustomProviders.RemoveAll(p => p.Id == id);
        settingsService.Save();
    }
}
