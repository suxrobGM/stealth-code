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
        var context = settingsService.Settings.Context;
        var instructions = capture.SystemPrompt;

        // Off the UI thread: the blit, the restore wait, PNG encoding, and the base64 encode all block.
        var prompt = await Task.Run<string?>(() =>
        {
            var imagePath = screenCaptureService.Capture(capture);
            if (imagePath is null)
            {
                return null;
            }

            imagePath = imagePath.Replace('\\', '/');

            var image = provider.ImageMode == ImageInputMode.Base64
                ? Convert.ToBase64String(File.ReadAllBytes(imagePath)) : imagePath;
            return CandidateContextPromptBuilder.ForCapture(instructions, null, [image], provider.ImageMode, context);
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
        var multiCaptureSystemPrompt = capture.MultiCaptureSystemPrompt;
        var instructions = capture.SystemPrompt;
        var context = settingsService.Settings.Context;

        string[] paths = [.. pendingCaptures];
        pendingCaptures.Clear();

        // Off the UI thread: reading and base64-encoding every pending PNG blocks.
        var prompt = await Task.Run(() =>
        {
            var images = provider.ImageMode == ImageInputMode.Base64
                ? paths.Select(path => Convert.ToBase64String(File.ReadAllBytes(path))).ToArray() : paths;
            return CandidateContextPromptBuilder.ForCapture(instructions, multiCaptureSystemPrompt, images, provider.ImageMode, context);
        });

        _ = PromptInjector.SendAsync(pty, prompt);
    }

    private static void Toast(string text, StatusLevel level = StatusLevel.Info) =>
        WeakReferenceMessenger.Default.Send(new ShowToastMessage(text, level));
}
