# Architecture

Developer reference for contributing to Stealth Code. For a feature-level overview, see [How It Works](how-it-works.md).

## Tech Stack

| Layer | Technology |
| --- | --- |
| Framework | .NET 10, PublishAot, full trim |
| UI | Avalonia 12.1 with Fluent theme |
| MVVM | CommunityToolkit.Mvvm (source generators) |
| DI | Microsoft.Extensions.DependencyInjection |
| Messaging | `WeakReferenceMessenger` (explicit `Register<T>` — `RegisterAll` is AOT incompatible) |
| Terminal | xterm.js in WebView2 (`Avalonia.Controls.WebView`) |
| PTY | ConPTY via Quick.PtyNet |
| Screen capture | Win32 GDI (`BitBlt`, `PrintWindow`) |
| Audio capture | WASAPI loopback (Win32 COM interop) |
| Transcription | Whisper.net (whisper.cpp), CPU backend by default |

## Project Structure

```text
src/
  StealthCode/                  # Composition root — UI, view models, orchestrators
    Controls/                   # Custom Avalonia controls (TerminalWebView, HotkeyTextBox)
    Messages/                   # WeakReferenceMessenger message types
    Models/                     # AppSettings, CliProviderConfig
    Services/                   # Orchestrators, hotkey, settings, content protection
    Themes/                     # Theme.axaml (colors), Styles.axaml (control styles)
    ViewModels/                 # MVVM view models
    Views/                      # Avalonia windows and user controls
  StealthCode.Terminal/         # PTY library + xterm.js assets
  StealthCode.ScreenCapture/    # GDI capture + PNG encoder
  StealthCode.Audio/            # WASAPI capture + Whisper transcription
  StealthCode.Updater/          # GitHub release checker + download
  StealthCode.Launcher/         # Self-extracting AOT launcher
scripts/
  publish.ps1                   # Build + package pipeline
```

## Module Design

Modules (Terminal, ScreenCapture, Audio, Updater) are **independent** — zero inter-module dependencies. Each module exposes a DI registration extension:

```csharp
services.AddTerminal();
services.AddScreenCapture();
services.AddAudioCapture();
services.AddUpdater();
```

Stateful services are registered as **singletons**. Stateless services are **static classes** (no DI needed).

Orchestrator services (`CaptureInjectorService`, `AudioInjectorService`) live in the main app and bridge modules with the terminal — modules never reference each other.

## Coding Conventions

**C# style:**

- File-scoped namespaces, braces required
- `[ObservableProperty]` for bindable properties, `[RelayCommand]` for commands
- Private fields: `camelCase`. Consts/statics: `PascalCase`
- Code-behind kept minimal — only platform-specific ops that can't live in view models
- Content protection disabled in DEBUG builds

**Win32 interop:**

- `[LibraryImport]` with `partial` static classes (not legacy `[DllImport]`)
- P/Invoke grouped by API surface (e.g., `User32`, `Gdi32`)

**XAML:**

- All colors defined in `Themes/Theme.axaml`
- All styles defined in `Themes/Styles.axaml`
- Reusable style classes: `Button.chrome`, `Button.chrome.pinned`, `TextBlock.section-header`, `TextBlock.field-label`, `Border.divider`, `*.form-input`

**Data storage:**

- Settings, models, and captures stored in `%APPDATA%/StealthCode/`
- Serialization via `System.Text.Json` source generators (AOT-safe)

## NativeWebView Caveat

`NativeWebView` renders in its own OS HWND — it floats above all Avalonia controls in Z-order. This has two implications:

1. **Layout** — Never overlay Avalonia controls on top of the WebView. Use side-by-side layout instead.
2. **Opacity** — Avalonia's `Window.Opacity` won't affect it. Window opacity is applied via Win32 `SetLayeredWindowAttributes` on the top-level HWND.

## Build & Run

```bash
dotnet build src/StealthCode/StealthCode.csproj
dotnet run --project src/StealthCode/StealthCode.csproj
```

### Publishing

```powershell
cd scripts
.\publish.ps1
```

Outputs a single `stealthcode.exe` at `publish/win-x64/`. See [How It Works — Launcher](how-it-works.md#launcher-single-file-distribution) for details on the self-extracting mechanism.
