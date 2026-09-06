using System.Reflection;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using Pty.Net;

namespace StealthCode.Terminal.Pty;

/// <summary>Raised when a terminal process cannot be started.</summary>
internal sealed class PtyStartException(string command, Exception innerException)
    : Exception($"Failed to start '{command}': {innerException.Message}", innerException);

/// <summary>
/// Runs terminal processes through ConPTY, the terminal support built into Windows. One process at a time:
/// starting a new one shuts down the one before it, and only the newest process's exit is reported. Each run
/// is numbered so an old process reporting its exit a moment after being replaced can be recognised as stale.
/// </summary>
[SupportedOSPlatform("windows")]
internal sealed class ConPtyProvider : IPtyProvider
{
    static ConPtyProvider()
    {
        // Pty.Net looks for a copy of conpty.dll that the Quick.PtyNet package does not include, so every
        // start would fail. Windows' own kernel32 offers the same functions, so send those lookups there.
        NativeLibrary.SetDllImportResolver(typeof(PtyProvider).Assembly, ResolveConPty);
    }

    private static IntPtr ResolveConPty(string libraryName, Assembly assembly, DllImportSearchPath? searchPath) =>
        // Pty.Net prefixes the name with an architecture folder, so only the file name is matched.
        Path.GetFileName(libraryName).Equals("conpty.dll", StringComparison.OrdinalIgnoreCase)
            ? NativeLibrary.Load("kernel32.dll")
            : IntPtr.Zero;

    /// <summary>How long to wait for the real exit code before falling back to a guess.</summary>
    private const int EofExitGraceMs = 500;

    private IPtyConnection? connection;
    private int generation;
    private int exitRaisedGeneration;

    /// <summary>Raised when the running process produces output.</summary>
    public event Action<byte[]>? OutputReceived;

    /// <summary>Raised once for the current process only; exits from replaced ones are ignored.</summary>
    public event Action<int>? ProcessExited;

    /// <summary>Starts a process, shutting down any running one.</summary>
    /// <exception cref="PtyStartException">The process could not be started.</exception>
    public void Start(string command, string[] args, string workingDirectory, int cols, int rows)
    {
        Stop();

        var gen = Interlocked.Increment(ref generation);

        IPtyConnection spawned;
        try
        {
            spawned = Task.Run(() => PtyProvider.SpawnAsync(
                BuildOptions(command, args, workingDirectory, cols, rows), CancellationToken.None))
                .GetAwaiter().GetResult();
        }
        catch (Exception ex)
        {
            throw new PtyStartException(command, ex);
        }

        // Subscribed before the connection is published, so an immediate exit is still reported.
        spawned.ProcessExited += (_, e) => NotifyExit(gen, e.ExitCode);
        Interlocked.Exchange(ref connection, spawned);

        new Thread(() => ReadLoop(spawned, gen))
        {
            IsBackground = true,
            Name = "ConPTY-Read"
        }.Start();
    }

    public void Write(byte[] data)
    {
        var active = Volatile.Read(ref connection);
        if (active is null)
        {
            return;
        }

        try
        {
            active.WriterStream.Write(data, 0, data.Length);
            active.WriterStream.Flush();
        }
        catch
        {
            // Gone or replaced while writing; the exit event handles the rest.
        }
    }

    public void Resize(int cols, int rows)
    {
        var active = Volatile.Read(ref connection);
        if (active is null || cols <= 0 || rows <= 0)
        {
            return;
        }

        try
        {
            active.Resize(cols, rows);
        }
        catch
        {
            // Usually a process that has just exited.
        }
    }

    /// <summary>Shuts down the running process, if any, so its exit will be ignored.</summary>
    public void Stop()
    {
        // Take the read loop and the exit handler out of service before closing the stream they are using.
        Interlocked.Increment(ref generation);

        var old = Interlocked.Exchange(ref connection, null);
        if (old is null)
        {
            return;
        }

        // Both throw if the process already exited, which still counts as a successful shutdown.
        BestEffort(old.Kill);
        BestEffort(old.Dispose);
    }

    public void Dispose() => Stop();

    private static void BestEffort(Action action)
    {
        try
        {
            action();
        }
        catch
        {
            // Shutting down either way.
        }
    }

    private void ReadLoop(IPtyConnection active, int gen)
    {
        var buffer = new byte[4096];
        try
        {
            while (true)
            {
                var bytesRead = active.ReaderStream.Read(buffer, 0, buffer.Length);
                if (bytesRead <= 0)
                {
                    RaiseFallbackExit(gen, active);
                    return;
                }

                if (Volatile.Read(ref generation) != gen)
                {
                    return;
                }

                OutputReceived?.Invoke(buffer.AsSpan(0, bytesRead).ToArray());
            }
        }
        catch when (Volatile.Read(ref generation) != gen)
        {
            // Stop closed this run's stream.
        }
        catch
        {
            // A closed pipe can throw instead of simply ending, and letting it escape would kill the app.
            RaiseFallbackExit(gen, active);
        }
    }

    /// <summary>
    /// Pty.Net's exit event is missed if the process dies before <see cref="Start"/> subscribes, so the
    /// output stream ending counts as an exit too. <see cref="NotifyExit"/> reports only one of the two.
    /// </summary>
    private void RaiseFallbackExit(int gen, IPtyConnection active)
    {
        // Returns as soon as the real exit code is available, rather than always waiting the full grace.
        BestEffort(() => active.WaitForExit(EofExitGraceMs));
        NotifyExit(gen, TryGetExitCode(active));
    }

    /// <summary>Reports one exit per run, and only while that run is still the current one.</summary>
    private void NotifyExit(int gen, int exitCode)
    {
        if (Volatile.Read(ref generation) != gen)
        {
            return;
        }

        if (Interlocked.Exchange(ref exitRaisedGeneration, gen) == gen)
        {
            return;
        }

        ProcessExited?.Invoke(exitCode);
    }

    private static int TryGetExitCode(IPtyConnection connection)
    {
        try
        {
            return connection.ExitCode;
        }
        catch
        {
            return -1;
        }
    }

    private static PtyOptions BuildOptions(
        string command, string[] args, string workingDirectory, int cols, int rows)
    {
        var env = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            // ConPTY passes escape codes straight through, so the CLIs get full colour.
            ["TERM"] = "xterm-256color",
            ["COLORTERM"] = "truecolor",
            ["LANG"] = "C.UTF-8",
            ["LC_ALL"] = "C.UTF-8",
            ["PYTHONUTF8"] = "1",
        };

        // Pty.Net merges this over the app's own environment, which is where the PATH repair lands.
        foreach (var (key, value) in PtyEnvironment.Overrides)
        {
            env[key] = value;
        }

        return new PtyOptions
        {
            App = command,
            CommandLine = args,
            Cwd = workingDirectory,
            Cols = cols,
            Rows = rows,
            ForceWinPty = false,
            Environment = env,
        };
    }
}
