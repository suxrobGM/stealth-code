using Microsoft.Extensions.DependencyInjection;
using StealthCode.Terminal.Pty;

namespace StealthCode.Terminal;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddTerminal(this IServiceCollection services)
    {
        services.AddSingleton<PtyService>();
        return services;
    }
}
