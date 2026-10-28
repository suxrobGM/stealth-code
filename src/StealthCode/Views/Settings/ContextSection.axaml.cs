using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using StealthCode.ViewModels.Settings;

namespace StealthCode.Views.Settings;

public partial class ContextSection : UserControl
{
    public ContextSection()
    {
        InitializeComponent();
    }

    private async void OnImportResumeClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not ContextSettingsViewModel viewModel || viewModel.IsImporting)
        {
            return;
        }

        var storage = TopLevel.GetTopLevel(this)?.StorageProvider;
        if (storage?.CanOpen != true)
        {
            viewModel.Error = "File selection is unavailable. Paste your background instead.";
            return;
        }

        try
        {
            var session = viewModel.SessionVersion;
            var files = await storage.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title = "Import resume",
                AllowMultiple = false,
                FileTypeFilter = [new FilePickerFileType("Resume documents") { Patterns = ["*.pdf", "*.docx", "*.txt"] }]
            });
            if (files.Count == 0 || session != viewModel.SessionVersion)
            {
                foreach (var selected in files)
                {
                    selected.Dispose();
                }
                return;
            }

            using var file = files[0];
            await using var stream = await file.OpenReadAsync();
            if (session == viewModel.SessionVersion)
            {
                await viewModel.ImportAsync(file.Name, stream);
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or NotSupportedException)
        {
            viewModel.Error = "Could not open this resume. Retry or paste your background instead.";
        }
    }
}
