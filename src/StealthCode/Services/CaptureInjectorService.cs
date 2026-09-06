using System.Text;
using StealthCode.ScreenCapture.Models;
using StealthCode.ScreenCapture.Services;
using StealthCode.Terminal;

namespace StealthCode.Services;

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

    public bool IsMultiCaptureActive => pendingCaptures.Count > 0;
    public int PendingCount => pendingCaptures.Count;

    /// <summary>
    ///     If multi-capture is active, finalizes and sends all accumulated screenshots.
    ///     Otherwise, captures a single screenshot and sends it immediately.
    /// </summary>
    public void CaptureAndInject()
    {
        if (pendingCaptures.Count > 0)
        {
            FinalizeMultiCapture();
            return;
        }

        var provider = providerRegistry.GetActiveProvider();
        if (!provider.SupportsImageInput)
        {
            return;
        }

        var capture = settingsService.Settings.Capture;
        var imagePath = screenCaptureService.Capture(capture)
            .Replace('\\', '/');

        var prompt = provider.ImageMode switch
        {
            ImageInputMode.FilePath =>
                $"{capture.SystemPrompt.Trim()} See the screenshot: {imagePath}",
            ImageInputMode.Base64 =>
                $"{capture.SystemPrompt.Trim()} [base64:{Convert.ToBase64String(File.ReadAllBytes(imagePath))}]",
            _ =>
                $"{capture.SystemPrompt.Trim()} See the screenshot: {imagePath}"
        };

        _ = PromptInjector.SendAsync(pty, prompt);
    }

    /// <summary>
    ///     Takes a screenshot and adds it to the pending multi-capture list.
    ///     Each press accumulates another screenshot. Use CaptureAndInject (Ctrl+Shift+C) to finalize.
    /// </summary>
    public void MultiCapture()
    {
        var capture = settingsService.Settings.Capture;
        var imagePath = screenCaptureService.Capture(capture).Replace('\\', '/');
        pendingCaptures.Add(imagePath);
    }

    private void FinalizeMultiCapture()
    {
        var provider = providerRegistry.GetActiveProvider();
        if (!provider.SupportsImageInput)
        {
            pendingCaptures.Clear();
            return;
        }

        var capture = settingsService.Settings.Capture;
        var multiCaptureSystemPrompt = capture.MultiCaptureSystemPrompt.Trim();

        var sb = new StringBuilder();
        sb.AppendLine(capture.SystemPrompt.Trim()); // Include the original prompt for context
        sb.Append(multiCaptureSystemPrompt);

        for (var i = 0; i < pendingCaptures.Count; i++)
        {
            var path = pendingCaptures[i];
            sb.Append(provider.ImageMode switch
            {
                ImageInputMode.FilePath => $" Screenshot {i + 1}: {path}",
                ImageInputMode.Base64 => $" Screenshot {i + 1}: [base64:{Convert.ToBase64String(File.ReadAllBytes(path))}]",
                _ => $" Screenshot {i + 1}: {path}"
            });
        }

        pendingCaptures.Clear();

        var prompt = sb.ToString();
        _ = PromptInjector.SendAsync(pty, prompt);
    }
}
