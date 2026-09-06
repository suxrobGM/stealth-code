using System.Text.Json.Serialization;

namespace StealthCode.Audio.Models;

/// <summary>Whisper runtime to use. GPU options need a downloaded pack.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<GpuBackend>))]
public enum GpuBackend
{
    /// <summary>CPU runtime included with the app.</summary>
    None = 0,

    /// <summary>GPU runtime for recent graphics cards.</summary>
    Vulkan = 1,

    /// <summary>NVIDIA runtime with automatic CUDA version selection.</summary>
    Cuda = 2,

    /// <summary>NVIDIA CUDA 12 runtime for older cards or drivers.</summary>
    Cuda12 = 3
}

public sealed record AudioSettings
{
    public string Hotkey { get; set; } = "Ctrl+Shift+A";
    public string ModelPath { get; set; } = AudioPaths.DefaultModel;

    /// <summary>Whisper runtime to use. GPU options use the CPU until their pack is installed.</summary>
    public GpuBackend GpuBackend { get; set; }

    /// <summary>Spoken language as a two-letter code, or "auto" to detect it.</summary>
    public string Language { get; set; } = "en";

    /// <summary>Silence that ends an utterance and submits it to the CLI.</summary>
    public int EndOfUtteranceMs { get; set; } = 1800;

    public string SystemPrompt { get; set; } =
        "Below is a live transcript of what the interviewer just said. Answer the question or solve the problem directly and concisely. For coding problems give the solution. Do not summarize or repeat the transcript.";
}
