namespace StealthCode.Audio.Models;

public sealed record AudioSettings
{
    public string Hotkey { get; set; } = "Ctrl+Shift+A";
    public string ModelPath { get; set; } = AudioPaths.DefaultModel;

    public string SystemPrompt { get; set; } =
        "Listen to the transcribed audio and answer any questions or problems concisely. For interview questions, give direct answers. For coding problems, provide the solution. Do not summarize the transcript unless asked.";
}
