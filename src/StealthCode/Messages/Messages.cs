using StealthCode.Models;
using StealthCode.ScreenCapture.Models;

namespace StealthCode.Messages;

// Settings panel -> MainWindowViewModel
public sealed record OpacityChangedMessage(double Opacity);

// MainWindowViewModel -> View
public sealed record SwitchTerminalMessage(CliProviderConfig Provider);
public sealed record FallbackToShellMessage;
public sealed record ApplyOpacityMessage(double Opacity);

// Settings -> MainWindow: re-register hotkeys
public sealed record HotkeyChangedMessage(string Name, string Hotkey);

// Settings panel -> MainWindow: request UI dialogs
public sealed record RequestRegionSelectionMessage;
public sealed record RequestWindowSelectionMessage;

// MainWindowViewModel -> View: listening state changed
public sealed record AudioRecordingChangedMessage(bool IsListening);

// MainWindowViewModel -> View: no-focus mode changed
public sealed record NoFocusChangedMessage(bool IsNoFocus);

// Settings panel -> AudioViewModel: the chosen Whisper model changed
public sealed record AudioModelChangedMessage(string ModelPath);

// Anything -> MainWindow: transient notice over the terminal
public sealed record ShowToastMessage(string Text, StatusLevel Level = StatusLevel.Info);

// Update notifications
public sealed record UpdateAvailableMessage(bool Available);

// MainWindow -> settings panel: dialog results
public sealed record RegionSelectedMessage(int X, int Y, int Width, int Height);
public sealed record WindowSelectedMessage(nint Handle, string Title);
