using System.Runtime.Versioning;

namespace StealthCode.Terminal;

/// <summary>
/// Rebuilds the PATH given to terminal processes. The launcher starts the app from an extracted copy, and
/// the environment it passes on can be missing the system-wide entries, which would leave the terminal
/// unable to find the CLI it is meant to run.
/// </summary>
[SupportedOSPlatform("windows")]
internal static class PtyEnvironment
{
    /// <summary>Windows' own default, used when the environment has no PATHEXT of its own.</summary>
    private const string DefaultPathExt = ".COM;.EXE;.BAT;.CMD;.VBS;.VBE;.JS;.JSE;.WSF;.WSH;.MSC";

    private const StringSplitOptions CleanSplit =
        StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries;

    /// <summary>
    /// The settings to merge into a terminal process's environment. Built once, because neither the
    /// machine-wide entries nor the app's own environment change while it runs.
    /// </summary>
    public static Dictionary<string, string> Overrides { get; } = Build();

    /// <summary>Inherited PATH first, then whatever the machine has that it is missing, then system folders.</summary>
    private static Dictionary<string, string> Build()
    {
        var root = Environment.GetEnvironmentVariable("SystemRoot") is { Length: > 0 } systemRoot
            ? systemRoot
            : @"C:\Windows";

        var comSpec = Environment.GetEnvironmentVariable("ComSpec");
        var pathExt = Environment.GetEnvironmentVariable("PATHEXT");

        string?[] pathSources =
        [
            Environment.GetEnvironmentVariable("PATH"),
            ReadPath(EnvironmentVariableTarget.Machine),
            ReadPath(EnvironmentVariableTarget.User),
            $@"{root}\System32;{root};{root}\System32\Wbem;{root}\System32\WindowsPowerShell\v1.0",
        ];

        return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["PATH"] = string.Join(';', pathSources
                .Where(p => !string.IsNullOrEmpty(p))
                .SelectMany(p => p!.Split(';', CleanSplit))
                .DistinctBy(entry => entry.TrimEnd('\\'), StringComparer.OrdinalIgnoreCase)),
            ["SystemRoot"] = root,
            ["ComSpec"] = string.IsNullOrEmpty(comSpec) ? $@"{root}\System32\cmd.exe" : comSpec,
            ["PATHEXT"] = string.IsNullOrEmpty(pathExt) ? DefaultPathExt : pathExt,
        };
    }

    private static string? ReadPath(EnvironmentVariableTarget target)
    {
        try
        {
            return Environment.GetEnvironmentVariable("Path", target);
        }
        catch
        {
            // Nothing readable here just means the inherited PATH has to do.
            return null;
        }
    }
}
