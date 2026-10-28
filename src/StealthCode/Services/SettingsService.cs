using System.Text.Json;
using System.Text.Json.Serialization;
using CommunityToolkit.Mvvm.Messaging;
using StealthCode.Messages;
using StealthCode.Models;

namespace StealthCode.Services;

[JsonSerializable(typeof(AppSettings))]
[JsonSourceGenerationOptions(WriteIndented = true, PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
internal sealed partial class AppSettingsJsonContext : JsonSerializerContext;

public sealed class SettingsService
{
    private readonly string settingsPath;
    private readonly Lock saveLock = new();
    private Timer? saveTimer;
    private long saveRevision;

    public SettingsService() : this(Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "StealthCode", "settings.json"))
    {
    }

    internal SettingsService(string settingsPath)
    {
        this.settingsPath = settingsPath;
        Settings = Load(settingsPath);
    }

    public AppSettings Settings { get; }

    public void Save()
    {
        lock (saveLock)
        {
            Write(Settings);
        }
    }

    /// <summary>Publishes a profile only after it has been written successfully.</summary>
    public void SaveContext(CandidateContextSettings context)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (context.CharacterCount > CandidateContextSettings.MaxCharacters)
        {
            throw new ArgumentException($"Keep saved context within {CandidateContextSettings.MaxCharacters:N0} characters.", nameof(context));
        }

        lock (saveLock)
        {
            Write(Settings with { Context = context });
            Settings.Context = context;
        }
    }

    private void Write(AppSettings settings)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(settingsPath))!);
        var json = JsonSerializer.Serialize(settings, AppSettingsJsonContext.Default.AppSettings);
        var tempPath = settingsPath + ".tmp";
        try
        {
            File.WriteAllText(tempPath, json);
            File.Move(tempPath, settingsPath, true);
        }
        finally
        {
            if (File.Exists(tempPath))
            {
                File.Delete(tempPath);
            }
        }
    }

    public void SaveDebounced()
    {
        lock (saveLock)
        {
            var revision = ++saveRevision;
            saveTimer?.Dispose();
            saveTimer = new Timer(_ =>
            {
                lock (saveLock)
                {
                    if (revision != saveRevision)
                    {
                        return;
                    }

                    try
                    {
                        Write(Settings);
                    }
                    catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                    {
                        WeakReferenceMessenger.Default.Send(new ShowToastMessage("Could not save settings", StatusLevel.Error));
                    }
                    finally
                    {
                        saveTimer?.Dispose();
                        saveTimer = null;
                    }
                }
            }, null, 500, Timeout.Infinite);
        }
    }

    private static AppSettings Load(string settingsPath)
    {
        if (!File.Exists(settingsPath))
        {
            return new AppSettings();
        }

        try
        {
            var json = File.ReadAllText(settingsPath);
            var settings = JsonSerializer.Deserialize(json, AppSettingsJsonContext.Default.AppSettings) ?? new AppSettings();
            var context = settings.Context ?? new CandidateContextSettings();
            context = context with
            {
                Background = context.Background ?? "",
                ExperienceStories = context.ExperienceStories ?? "",
                Opportunity = context.Opportunity ?? ""
            };
            settings.Context = context.CharacterCount <= CandidateContextSettings.MaxCharacters
                ? context
                : context with { Enabled = false };
            return settings;
        }
        catch
        {
            return new AppSettings();
        }
    }
}
