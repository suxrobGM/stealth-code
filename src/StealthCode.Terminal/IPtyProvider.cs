namespace StealthCode.Terminal;

/// <summary>
/// Runs one terminal process at a time. Starting a new process shuts down the running one, and only the
/// newest process's exit is reported. Also keeps <see cref="PtyService"/> off a Windows-only type, so the
/// platform check stays in one place.
/// </summary>
internal interface IPtyProvider : IDisposable
{
    /// <summary>Raised when the running process produces output.</summary>
    event Action<byte[]>? OutputReceived;

    /// <summary>Raised once for the current process; exits from processes it replaced are ignored.</summary>
    event Action<int>? ProcessExited;

    /// <summary>Starts a process, shutting down any running one.</summary>
    /// <exception cref="PtyStartException">The process could not be started.</exception>
    void Start(string command, string[] args, string workingDirectory, int cols, int rows);

    void Write(byte[] data);

    void Resize(int cols, int rows);

    /// <summary>Shuts down the running process, if any, so its exit will be ignored.</summary>
    void Stop();
}
