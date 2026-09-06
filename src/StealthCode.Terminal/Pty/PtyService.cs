using System.Text;

namespace StealthCode.Terminal.Pty;

/// <summary>
/// The terminal's way in. Keeps one provider for as long as the app runs and restarts the process inside
/// it, so switching between CLIs never disturbs what the UI is connected to.
/// </summary>
public sealed class PtyService : IDisposable
{
    private readonly IPtyProvider provider = CreateProvider();

    public PtyService()
    {
        provider.OutputReceived += data => OutputReceived?.Invoke(data);
        provider.ProcessExited += exitCode => ProcessExited?.Invoke(exitCode);
    }

    public event Action<byte[]>? OutputReceived;

    /// <summary>Raised for the current process only; exits from replaced ones are ignored.</summary>
    public event Action<int>? ProcessExited;

    public void Start(string command, string[] args, string workingDirectory, int cols, int rows)
    {
        try
        {
            provider.Start(command, args, workingDirectory, cols, rows);
        }
        catch (PtyStartException ex)
        {
            OutputReceived?.Invoke(Encoding.UTF8.GetBytes($"\e[31m{ex.Message}\e[0m\r\n"));
            ProcessExited?.Invoke(-1);
        }
    }

    public void Write(byte[] data) => provider.Write(data);

    public void Resize(int cols, int rows) => provider.Resize(cols, rows);

    public void Stop() => provider.Stop();

    public void Dispose() => provider.Dispose();

    private static IPtyProvider CreateProvider()
    {
        if (!OperatingSystem.IsWindows())
        {
            throw new PlatformNotSupportedException("Only Windows is supported");
        }

        return new ConPtyProvider();
    }
}
