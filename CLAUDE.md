# StealthCode

A Windows desktop app (Avalonia UI) that runs AI coding CLIs (Claude Code, Codex, Gemini CLI) in a secure, transparent, always-on-top terminal window invisible to screen capture.

## Build & Run

```bash
dotnet build src/StealthCode/StealthCode.csproj
dotnet run --project src/StealthCode/StealthCode.csproj
```

Publish single executable (via Launcher):

```powershell
cd scripts
.\publish.ps1
```

No test projects exist yet.

## Architecture

- **Framework:** .NET 10, Avalonia 12.1, PublishAot, full trim
- **Platform:** Windows only
- **Pattern:** MVVM with CommunityToolkit.Mvvm (source generators)
- **DI:** Module registrar pattern — `services.AddTerminal()`, `services.AddScreenCapture()`, `services.AddAudioCapture()`. Stateful services are singletons. Stateless services are static classes.
- **Messaging:** `WeakReferenceMessenger` with explicit `Register<T>` (no `RegisterAll` — AOT incompatible)
- **Terminal:** xterm.js in `NativeWebView` (WebView2), bridged via ConPTY

## Project Structure

```text
src/
  StealthCode/                  # Main UI app (composition root, MVVM, orchestrators)
  StealthCode.Terminal/         # PTY library (ConPTY via Quick.PtyNet)
  StealthCode.ScreenCapture/    # Win32 screen capture (GDI BitBlt/PrintWindow, PngWriter)
  StealthCode.Audio/            # WASAPI loopback capture (Win32 interop) + Whisper.net transcription
  StealthCode.Updater/          # GitHub release checker + self-update downloader
  StealthCode.Launcher/         # Self-extracting AOT launcher
scripts/
  publish.ps1                   # Build + package pipeline
```

Modules (Terminal, ScreenCapture, Audio) are independent — zero inter-module dependencies. Orchestrator services (`CaptureInjectorService`, `AudioInjectorService`) live in the main app and bridge modules with the terminal.

## Key Conventions

- File-scoped namespaces, braces required
- `[ObservableProperty]` for bindable properties, `[RelayCommand]` for commands
- Private fields: camelCase. Consts/statics: PascalCase
- Win32 interop uses `[LibraryImport]` with `partial` static classes
- All colors in `Themes/Theme.axaml`, all styles in `Themes/Styles.axaml`
- Reusable XAML classes: `Button.chrome`, `Button.chrome.pinned`, `TextBlock.section-header`, `TextBlock.field-label`, `Border.divider`, `*.form-input`
- Settings, models, and captures stored in `%APPDATA%/StealthCode/` via `System.Text.Json` source gen
- Code-behind kept minimal — only platform-specific ops
- Content protection disabled in DEBUG builds

## NativeWebView Caveat

`NativeWebView` renders in its own OS HWND — it sits above all Avalonia controls. Never overlay Avalonia controls on top of it; use side-by-side layout instead. Window opacity requires Win32 `SetLayeredWindowAttributes` (Avalonia's `Window.Opacity` won't affect it).
