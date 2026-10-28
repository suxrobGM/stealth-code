using StealthCode.Audio.Models;
using StealthCode.ScreenCapture.Models;

namespace StealthCode.Models;

public sealed record class AppSettings
{
    private CandidateContextSettings context = new();

    public CandidateContextSettings Context
    {
        get => Volatile.Read(ref context);
        set => Volatile.Write(ref context, value);
    }

    public string ActiveProviderId { get; set; } = "claude-code";
    public double WindowOpacity { get; set; } = 1.0;
    public bool AlwaysOnTop { get; set; }
    public CaptureSettings Capture { get; set; } = new();
    public string OpacityHotkey { get; set; } = "Ctrl+Shift+O";
    public string NoFocusHotkey { get; set; } = "Ctrl+Shift+F";
    public AudioSettings Audio { get; set; } = new();
    public List<CliProviderConfig> CustomProviders { get; set; } = [];
}
