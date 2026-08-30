namespace StealthCode.Audio.Models;

public sealed record AudioSettings
{
    public string Hotkey { get; set; } = "Ctrl+Shift+A";
    public string ModelPath { get; set; } = AudioPaths.DefaultModel;

    /// <summary>
    /// Runs Whisper on the graphics card. Off by default, because a version that cannot load closes the app
    /// instead of falling back to the CPU. Switches itself off after a failed attempt, and only takes
    /// effect the next time the app starts.
    /// </summary>
    public bool UseGpu { get; set; }

    public string SystemPrompt { get; set; } =
        "Listen to the transcribed audio and answer any questions or problems concisely. For interview questions, give direct answers. For coding problems, provide the solution. Do not summarize the transcript unless asked.";
}
