using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using StealthCode.Models;

namespace StealthCode.ViewModels;

/// <summary>
/// The single status line in the action bar. A transient note wins while its timer runs; otherwise whatever is
/// still true shows through.
/// </summary>
public sealed partial class StatusBarViewModel : ViewModelBase
{
    private static readonly TimeSpan TransientDuration = TimeSpan.FromSeconds(1.5);

    private readonly AudioViewModel audio;
    private readonly DispatcherTimer transientTimer;
    private string? transientText;
    private StatusLevel transientLevel;

    public StatusBarViewModel(AudioViewModel audio)
    {
        this.audio = audio;

        transientTimer = new DispatcherTimer { Interval = TransientDuration };
        transientTimer.Tick += (_, _) =>
        {
            transientTimer.Stop();
            transientText = null;
            Refresh();
        };

        audio.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName is nameof(AudioViewModel.StatusText) or nameof(AudioViewModel.StatusLevel))
            {
                Refresh();
            }
        };
    }

    /// <summary>Screenshots waiting to be sent.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasPendingCaptures))]
    public partial int PendingCaptureCount { get; set; }

    public bool HasPendingCaptures => PendingCaptureCount > 0;

    public string Text => transientText ?? StandingText;

    public StatusLevel Level => transientText is not null ? transientLevel : StandingLevel;

    // Severity reaches the bar as style classes.
    public bool IsWarning => Level == StatusLevel.Warning;

    public bool IsError => Level == StatusLevel.Error;

    private string StandingText =>
        audio.StatusText.Length > 0 ? audio.StatusText
        : PendingCaptureCount > 0 ? $"{PendingCaptureCount} captured"
        : "";

    private StatusLevel StandingLevel =>
        audio.StatusText.Length > 0 ? audio.StatusLevel : StatusLevel.Info;

    /// <summary>Puts a line in the bar for a moment, then lets standing state show again.</summary>
    public void Show(string text, StatusLevel level = StatusLevel.Info)
    {
        transientText = text;
        transientLevel = level;
        Refresh();

        transientTimer.Stop();
        transientTimer.Start();
    }

    partial void OnPendingCaptureCountChanged(int value) => Refresh();

    private void Refresh()
    {
        OnPropertyChanged(nameof(Text));
        OnPropertyChanged(nameof(Level));
        OnPropertyChanged(nameof(IsWarning));
        OnPropertyChanged(nameof(IsError));
    }
}
