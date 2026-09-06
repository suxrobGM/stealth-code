using System.Text;
using CommunityToolkit.Mvvm.Messaging;
using StealthCode.Messages;
using StealthCode.Models;
using StealthCode.Terminal;

namespace StealthCode.Services;

/// <summary>Sends a prompt into the terminal as one bracketed paste followed by Enter.</summary>
public static class PromptInjector
{
    /// <summary>Codex on Windows sees the paste as a key burst and ignores Enter for ~200 ms after it; 500 ms is safe.</summary>
    public const int EnterDelayMs = 500;

    private static readonly SemaphoreSlim SendLock = new(1, 1);
    private static readonly byte[] Enter = "\r"u8.ToArray();

    /// <summary>Strips control characters (keeping tab and newline) and pastes the prompt, then presses Enter.</summary>
    public static async Task SendAsync(PtyService pty, string prompt)
    {
        var cleaned = new StringBuilder(prompt.Length);
        foreach (var c in prompt)
        {
            if (c is '\n' or '\t' || !char.IsControl(c))
            {
                cleaned.Append(c);
            }
        }

        var pasteBytes = Encoding.UTF8.GetBytes($"\x1b[200~{cleaned}\x1b[201~");

        await SendLock.WaitAsync();

        try
        {
            pty.Write(pasteBytes);
            await Task.Delay(EnterDelayMs);
            pty.Write(Enter);
            WeakReferenceMessenger.Default.Send(new ShowToastMessage("Sent", StatusLevel.Success));
        }
        finally
        {
            SendLock.Release();
        }
    }
}
