using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using Avalonia.VisualTree;
using StealthCode.Audio.Downloads;
using StealthCode.Services;
using StealthCode.Updater.Services;
using StealthCode.ViewModels.Settings;
using StealthCode.Views.Settings;

namespace StealthCode.Tests;

public sealed class TestAppBuilder
{
    public static AppBuilder BuildAvaloniaApp() => AppBuilder.Configure<App>()
        .UseSkia().WithInterFont()
        .UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false });
}

public sealed class ContextLayoutTests
{
    [Fact]
    public async Task ContextFitsSettingsPanelAndDraftSurvivesTabSwitching()
    {
        var session = HeadlessUnitTestSession.StartNew(typeof(TestAppBuilder));
        try
        {
            await session.Dispatch(CheckContextLayout, TestContext.Current.CancellationToken);
        }
        finally
        {
            // Dispatch can complete inline on its own worker. Dispose must join it from another thread.
            await Task.Run(session.Dispose, CancellationToken.None);
        }
    }

    private void CheckContextLayout()
    {
        var directory = Path.Combine(Path.GetTempPath(), "StealthCode.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        var settings = new SettingsService(Path.Combine(directory, "settings.json"));
        using var gpuService = new GpuPackService();
        using var modelService = new ModelDownloadService();
        using var updateService = new UpdateService();
        var context = new ContextSettingsViewModel(settings);
        var vm = new SettingsViewModel(new GeneralSettingsViewModel(settings),
            new CaptureSettingsViewModel(settings, new CliProviderRegistry(settings)),
            new AudioSettingsViewModel(settings, new WhisperModelInstaller(modelService, settings),
                new GpuPackSettingsViewModel(settings, gpuService)), context,
            new UpdateSettingsViewModel(updateService));
        vm.Load();
        vm.SelectedTab = SettingsTab.Context;
        var view = new SettingsView { DataContext = vm };
        var window = new Window { Width = 320, Height = 544, Content = view };
        try
        {
            window.Show();
            Dispatcher.UIThread.RunJobs();
            var tabs = view.GetVisualDescendants().OfType<Button>().Where(b => b.Classes.Contains("tab")).ToArray();
            Assert.Equal(4, tabs.Length);
            foreach (var tab in tabs)
            {
                var topLeft = tab.TranslatePoint(default, view)!.Value;
                Assert.True(topLeft.X >= 0 && topLeft.X + tab.Bounds.Width <= view.Bounds.Width,
                    $"Tab {tab.Content} exceeds settings width: {topLeft.X} + {tab.Bounds.Width}");
            }

            var section = view.GetVisualDescendants().OfType<ContextSection>().Single();
            var fields = section.GetVisualDescendants().OfType<TextBox>().Where(t => t.IsEffectivelyVisible).ToArray();
            Assert.Equal(3, fields.Length);
            fields[0].Text = "Alex, software engineer at Acme.";
            Dispatcher.UIThread.RunJobs();
            Assert.Equal(fields[0].Text, context.Background);
            Assert.Empty(settings.Settings.Context.Background);

            vm.SelectedTab = SettingsTab.Audio;
            vm.SelectedTab = SettingsTab.Context;
            Assert.Equal("Alex, software engineer at Acme.", context.Background);
            var scroll = view.GetVisualDescendants().OfType<ScrollViewer>().First(s => s.Content is Panel);
            Assert.True(scroll.Extent.Height > scroll.Viewport.Height);
            SaveFrame(window, "context-top.png");

            scroll.Offset = new Vector(0, scroll.Extent.Height);
            window.UpdateLayout();
            var save = section.GetVisualDescendants().OfType<Button>().Single(b => Equals(b.Content, "Save"));
            Assert.NotNull(save.Command);
            save.Command.Execute(save.CommandParameter);
            window.UpdateLayout();
            Assert.Equal("Alex, software engineer at Acme.", settings.Settings.Context.Background);
            Assert.False(settings.Settings.Context.Enabled);
            SaveFrame(window, "context-save.png");

            context.ImportPreview = "Imported resume to review before replacement.";
            context.HasImportPreview = true;
            scroll.Offset = new Vector(0, 240);
            window.UpdateLayout();
            SaveFrame(window, "context-import.png");
        }
        finally
        {
            window.Close();
            vm.Unload();
            Directory.Delete(directory, true);
        }
    }

    private static void SaveFrame(Window window, string filename)
    {
        var output = Path.Combine(AppContext.BaseDirectory, "ui-artifacts");
        Directory.CreateDirectory(output);
        using var frame = new RenderTargetBitmap(new PixelSize((int)window.Width, (int)window.Height), new Vector(96, 96));
        frame.Render((Visual)window.Content!);
        frame.Save(Path.Combine(output, filename), PngBitmapEncoderOptions.Default);
    }
}
