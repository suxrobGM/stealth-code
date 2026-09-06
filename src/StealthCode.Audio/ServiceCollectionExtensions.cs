using Microsoft.Extensions.DependencyInjection;
using StealthCode.Audio.Downloads;
using StealthCode.Audio.Transcription;

namespace StealthCode.Audio;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddAudioCapture(this IServiceCollection services)
    {
        services.AddSingleton<LiveTranscriptionService>();
        services.AddSingleton<TranscriptionService>();
        services.AddSingleton<ModelDownloadService>();
        services.AddSingleton<GpuPackService>();
        return services;
    }
}
