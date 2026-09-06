namespace StealthCode.Utilities;

/// <summary>Shortens a system prompt to a single line for a collapsed disclosure row.</summary>
public static class PromptPreview
{
    private const int MaxLength = 42;

    public static string Of(string prompt)
    {
        var flat = prompt.Replace('\n', ' ').Replace('\r', ' ').Trim();
        return flat.Length <= MaxLength ? flat : flat[..MaxLength].TrimEnd() + "...";
    }
}
