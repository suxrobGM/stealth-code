using System.Text;
using CommunityToolkit.Mvvm.Messaging;
using StealthCode.Messages;
using StealthCode.Models;
using StealthCode.ScreenCapture.Models;
using StealthCode.ScreenCapture.Services;
using StealthCode.Terminal.Pty;

namespace StealthCode.Services.Injection;

/// <summary>
///     Captures screenshots and injects them into the terminal.
///     Supports single-shot capture and multi-capture mode (accumulate then send).
/// </summary>
public sealed class CaptureInjectorService(
    SettingsService settingsService,
    CliProviderRegistry providerRegistry,
    ScreenCaptureService screenCaptureService,
    PtyService pty)
{
    private readonly List<string> pendingCaptures = [];

    public int PendingCount => pendingCaptures.Count;

    /// <summary>
    ///     If multi-capture is active, finalizes and sends all accumulated screenshots.
    ///     Otherwise, captures a single screenshot and sends it immediately.
    /// </summary>
    public async Task CaptureAndInjectAsync()
    {
        if (pendingCaptures.Count > 0)
        {
            await FinalizeMultiCaptureAsync();
            return;
        }

        var provider = providerRegistry.GetActiveProvider();
        if (!provider.SupportsImageInput)
        {
            Toast($"{provider.Name} cannot accept images", StatusLevel.Warning);
            return;
        }

        var capture = settingsService.Settings.Capture;

        // Off the UI thread: the blit, the restore wait, PNG encoding, and the base64 encode all block.
        var prompt = await Task.Run<string?>(() =>
        {
            var imagePath = screenCaptureService.Capture(capture);
            if (imagePath is null)
            {
                return null;
            }

            imagePath = imagePath.Replace('\\', '/');

            return provider.ImageMode switch
            {
                ImageInputMode.FilePath =>
                    $"{capture.SystemPrompt.Trim()} See the screenshot: {imagePath}",
                ImageInputMode.Base64 =>
                    $"{capture.SystemPrompt.Trim()} [base64:{Convert.ToBase64String(File.ReadAllBytes(imagePath))}]",
                _ =>
                    $"{capture.SystemPrompt.Trim()} See the screenshot: {imagePath}"
            };
        });

        if (prompt is null)
        {
            Toast("Capture failed", StatusLevel.Error);
            return;
        }

        _ = PromptInjector.SendAsync(pty, prompt);
    }

    /// <summary>
    ///     Takes a screenshot and adds it to the pending multi-capture list.
    ///     Each press accumulates another screenshot. Use CaptureAndInject (Ctrl+Shift+C) to finalize.
    /// </summary>
    public async Task MultiCaptureAsync()
    {
        var capture = settingsService.Settings.Capture;
        var imagePath = await Task.Run(() => screenCaptureService.Capture(capture));

        if (imagePath is null)
        {
            Toast("Capture failed", StatusLevel.Error);
            return;
        }

        pendingCaptures.Add(imagePath.Replace('\\', '/'));
        Toast($"{pendingCaptures.Count} captured");
    }

    private async Task FinalizeMultiCaptureAsync()
    {
        var provider = providerRegistry.GetActiveProvider();
        if (!provider.SupportsImageInput)
        {
            // Say so; the queue is discarded either way.
            var discarded = pendingCaptures.Count;
            pendingCaptures.Clear();
            Toast($"{provider.Name} cannot accept images — {discarded} discarded", StatusLevel.Warning);
            return;
        }

        var capture = settingsService.Settings.Capture;
        var multiCaptureSystemPrompt = capture.MultiCaptureSystemPrompt.Trim();

        string[] paths = [.. pendingCaptures];
        pendingCaptures.Clear();

        // Off the UI thread: reading and base64-encoding every pending PNG blocks.
        var prompt = await Task.Run(() =>
        {
            var sb = new StringBuilder();
            sb.AppendLine(capture.SystemPrompt.Trim()); // Include the original prompt for context
            sb.Append(multiCaptureSystemPrompt);

            for (var i = 0; i < paths.Length; i++)
            {
                var path = paths[i];
                sb.Append(provider.ImageMode switch
                {
                    ImageInputMode.FilePath => $" Screenshot {i + 1}: {path}",
                    ImageInputMode.Base64 => $" Screenshot {i + 1}: [base64:{Convert.ToBase64String(File.ReadAllBytes(path))}]",
                    _ => $" Screenshot {i + 1}: {path}"
                });
            }

            return sb.ToString();
        });

        _ = PromptInjector.SendAsync(pty, prompt);
    }

    private static void Toast(string text, StatusLevel level = StatusLevel.Info) =>
        WeakReferenceMessenger.Default.Send(new ShowToastMessage(text, level));
}
