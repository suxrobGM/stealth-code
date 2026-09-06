using System.Collections.Frozen;
using System.Text.RegularExpressions;

namespace StealthCode.Audio.Services;

/// <summary>Strips Whisper artefacts and known hallucinations from a segment.</summary>
internal static partial class TranscriptFilter
{
    private static readonly FrozenSet<string> Hallucinations = new[]
    {
        "thank you",
        "thanks for watching",
        "you",
        "bye",
        "subtitles by the amara org community"
    }.ToFrozenSet(StringComparer.OrdinalIgnoreCase);

    [GeneratedRegex(@"\[[^\]]*\]|\([^)]*\)")]
    private static partial Regex TagPattern();

    [GeneratedRegex(@"\s+")]
    private static partial Regex WhitespacePattern();

    [GeneratedRegex(@"[^\p{L}\p{N}]+")]
    private static partial Regex NonAlphanumericPattern();

    /// <summary>Returns the cleaned segment text, or empty when the segment should be dropped.</summary>
    public static string Clean(string text, float noSpeechProbability)
    {
        if (noSpeechProbability > 0.7f || string.IsNullOrWhiteSpace(text))
        {
            return string.Empty;
        }

        var cleaned = WhitespacePattern().Replace(TagPattern().Replace(text, " "), " ").Trim();

        // Empty once stripped means the segment held no letters or digits at all.
        var normalized = NonAlphanumericPattern().Replace(cleaned, " ").Trim();
        return normalized.Length == 0 || Hallucinations.Contains(normalized) ? string.Empty : cleaned;
    }
}
