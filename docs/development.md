# Development

## Build and run

Requires the .NET 10 SDK on Windows.

```bash
dotnet build src/StealthCode/StealthCode.csproj
dotnet run --project src/StealthCode/StealthCode.csproj
```

Publish the single executable to `publish/win-x64/`:

```powershell
cd scripts
.\publish.ps1
```

## Tech stack

| Layer | Technology |
| --- | --- |
| Runtime | .NET 10, native AOT, full trimming |
| UI | Avalonia 12.1, CommunityToolkit.Mvvm |
| Terminal | xterm.js in WebView2, ConPTY via Quick.PtyNet |
| Screen capture | Win32 GDI, custom PNG encoder |
| Audio | WASAPI loopback, Whisper.net |
| Updates | GitHub Releases API |

## Project layout

```text
src/
  StealthCode/                  Main app: UI, view models, orchestrators
  StealthCode.Terminal/         ConPTY plus embedded xterm.js
  StealthCode.ScreenCapture/    GDI capture and PNG writer
  StealthCode.Audio/            WASAPI loopback and Whisper transcription
  StealthCode.Updater/          Release check and self-update
  StealthCode.Launcher/         Self-extracting launcher
scripts/
  publish.ps1                   Build and package
  verify-gpu-packs.ps1          Hash check for GPU packs
```

The library projects are independent. Each exposes one `services.AddX()` method. `CaptureInjectorService` and `AudioInjectorService` in the main app bridge them to the terminal, and `PromptInjector` knows how to type a prompt into each CLI.

## Gotchas

- **WebView2 is a native child window.** Nothing Avalonia can overlay it, and `Window.Opacity` does not fade it. Opacity uses `SetLayeredWindowAttributes`.
- **Whisper.net aborts instead of falling back** when a runtime fails to load. The app offers exactly one runtime and uses marker files to remember a pack that crashed.
- **`RuntimeOptions.LibraryPath` is treated as a file path**, so the directory needs a trailing separator.
- **AOT needs `IlcInstructionSet=avx`** or Whisper's CPU backend refuses to load.
- **PTY processes are generation-tagged** so a late exit from the previous CLI is not read as a crash of the new one.
