using System.Text.Json;
using Avalonia.Controls;
using Avalonia.Platform;
using Avalonia.Threading;
using StealthCode.Models;
using StealthCode.Terminal;
using StealthCode.Terminal.Pty;
using StealthCode.Terminal.Web;

namespace StealthCode.Controls;

public sealed class TerminalWebView : UserControl, IDisposable
{
    private NativeWebView? webView;
    private PtyService? ptyService;
    private readonly List<byte> outputBuffer = [];
    private readonly Lock bufferLock = new();
    private bool terminalReady;
    private bool documentLoaded;
    private int pendingCols;
    private int pendingRows;

    private readonly Queue<string> pendingScripts = new();

    private string? pendingCommand;
    private string[]? pendingArgs;
    private string? pendingWorkingDir;

    public event Action<int>? ProcessExited;

    public void Initialize(PtyService ptyService)
    {
        this.ptyService = ptyService;
        this.ptyService.OutputReceived += OnPtyOutput;
        this.ptyService.ProcessExited += OnPtyProcessExited;

        webView = new NativeWebView();
        webView.EnvironmentRequested += OnEnvironmentRequested;
        webView.NavigationStarted += OnNavigationStarted;
        webView.NewWindowRequested += OnNewWindowRequested;
        webView.NavigationCompleted += OnNavigationCompleted;
        webView.WebMessageReceived += OnWebMessageReceived;

        Content = webView;

        webView.NavigateToString(
            TerminalAssets.GetTerminalHtml(TerminalScripts.ResolveTheme()), TerminalAssets.BaseUri);
    }

    public void StartProcess(string command, string[] args, string workingDirectory)
    {
        if (terminalReady && pendingCols > 0)
        {
            ptyService?.Start(command, args, workingDirectory, pendingCols, pendingRows);
        }
        else
        {
            pendingCommand = command;
            pendingArgs = args;
            pendingWorkingDir = workingDirectory;
        }
    }

    public void Reset()
    {
        webView?.InvokeScript(TerminalScripts.Reset());
    }

    /// <summary>Shows a notice inside the terminal document, the only surface that can float over the terminal.</summary>
    public void ShowToast(string message, StatusLevel level)
    {
        Invoke(TerminalScripts.Toast(message, level));
    }

    /// <summary>Runs a script once the terminal document is loaded, holding it in order until then.</summary>
    private void Invoke(string script)
    {
        if (documentLoaded)
        {
            webView?.InvokeScript(script);
        }
        else
        {
            pendingScripts.Enqueue(script);
        }
    }

    /// <summary>
    /// Gives the WebView host a throwaway profile under the temp directory instead of letting it keep a
    /// browsing profile next to the executable, so a session leaves nothing behind once it ends.
    /// </summary>
    private static void OnEnvironmentRequested(object? sender, WebViewEnvironmentRequestedEventArgs e)
    {
#if DEBUG
        e.EnableDevTools = true;
#endif

        if (e is WindowsWebView2EnvironmentRequestedEventArgs webView2)
        {
            webView2.UserDataFolder = TerminalPaths.WebViewProfile;
            webView2.IsInPrivateModeEnabled = true;
        }
    }

    /// <summary>
    /// Terminal output is untrusted text, so once the terminal document is up nothing is allowed to navigate
    /// this WebView again. The event carries no URI, so the first navigation — the one that loads the
    /// terminal itself — is the only one that can be told apart, and everything after it is refused.
    /// </summary>
    private void OnNavigationStarted(object? sender, WebViewNavigationStartingEventArgs e)
    {
        e.Cancel = documentLoaded;
    }

    /// <summary>Nothing printed to a terminal should be able to open a browser window.</summary>
    private static void OnNewWindowRequested(object? sender, WebViewNewWindowRequestedEventArgs e)
    {
        e.Handled = true;
    }

    private void OnNavigationCompleted(object? sender, WebViewNavigationCompletedEventArgs e)
    {
        if (e.IsSuccess)
        {
            documentLoaded = true;

            // Re-send ready in case invokeCSharpAction wasn't injected when terminal.js first ran
            webView?.InvokeScript(TerminalScripts.Ready());

            while (pendingScripts.Count > 0)
            {
                webView?.InvokeScript(pendingScripts.Dequeue());
            }
        }
    }

    private void OnWebMessageReceived(object? sender, WebMessageReceivedEventArgs e)
    {
        try
        {
            if (e.Body is null)
            {
                return;
            }

            using var doc = JsonDocument.Parse(e.Body);
            var root = doc.RootElement;
            var type = root.GetProperty("type").GetString();

            switch (type)
            {
                case "ready":
                    pendingCols = root.GetProperty("cols").GetInt32();
                    pendingRows = root.GetProperty("rows").GetInt32();
                    terminalReady = true;

                    if (pendingCommand is not null)
                    {
                        ptyService?.Start(pendingCommand, pendingArgs!, pendingWorkingDir!, pendingCols, pendingRows);
                        pendingCommand = null;
                        pendingArgs = null;
                        pendingWorkingDir = null;
                    }
                    break;

                case "input":
                    var data = root.GetProperty("data").GetString();
                    if (data is not null)
                    {
                        ptyService?.Write(System.Text.Encoding.UTF8.GetBytes(data));
                    }
                    break;

                case "resize":
                    var cols = root.GetProperty("cols").GetInt32();
                    var rows = root.GetProperty("rows").GetInt32();
                    pendingCols = cols;
                    pendingRows = rows;
                    ptyService?.Resize(cols, rows);
                    break;
            }
        }
        catch
        {
            // Ignore malformed messages
        }
    }

    private void OnPtyOutput(byte[] data)
    {
        lock (bufferLock)
        {
            if (outputBuffer.Count == 0)
            {
                Dispatcher.UIThread.InvokeAsync(FlushOutput, DispatcherPriority.Background);
            }

            outputBuffer.AddRange(data);
        }
    }

    private void FlushOutput()
    {
        byte[] data;
        lock (bufferLock)
        {
            if (outputBuffer.Count == 0)
            {
                return;
            }

            data = [.. outputBuffer];
            outputBuffer.Clear();
        }

        webView?.InvokeScript(TerminalScripts.Write(data));
    }

    private void OnPtyProcessExited(int exitCode)
    {
        Dispatcher.UIThread.InvokeAsync(() => ProcessExited?.Invoke(exitCode));
    }

    public void Dispose()
    {
        if (ptyService is not null)
        {
            ptyService.OutputReceived -= OnPtyOutput;
            ptyService.ProcessExited -= OnPtyProcessExited;
        }

        if (webView is not null)
        {
            webView.EnvironmentRequested -= OnEnvironmentRequested;
            webView.NavigationStarted -= OnNavigationStarted;
            webView.NewWindowRequested -= OnNewWindowRequested;
            webView.NavigationCompleted -= OnNavigationCompleted;
            webView.WebMessageReceived -= OnWebMessageReceived;
            Content = null;
            webView = null;
        }

        // The host holds the profile open until it is torn down, so this generally clears a previous
        // session's folder and leaves this one for the next launch to collect.
        TerminalPaths.CleanupProfiles();
    }
}
