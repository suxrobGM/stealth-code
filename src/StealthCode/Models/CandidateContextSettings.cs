using System.Text.Json.Serialization;

namespace StealthCode.Models;

/// <summary>An immutable snapshot of the reviewed profile used by subsequent requests.</summary>
public sealed record CandidateContextSettings
{
    public const int MaxCharacters = 32_000;

    public bool Enabled { get; init; }
    public string Background { get; init; } = "";
    public string ExperienceStories { get; init; } = "";
    public string Opportunity { get; init; } = "";

    [JsonIgnore]
    public int CharacterCount => Background.Length + ExperienceStories.Length + Opportunity.Length;
}
