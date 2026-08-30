# Changelog

All notable changes to Stealth Code will be documented in this file.

## [Unreleased]

### Fixes

- Whisper now works in published builds - ILC's default x64 baseline omits AVX, so `Avx.IsSupported` was always false and Whisper.net refused its CPU backend. Fixed with `IlcInstructionSet=avx`
- Whisper no longer kills the app when CUDA fails to load - a failed load aborts the process instead of falling back, so the backend is pinned to a single runtime, CPU by default
- Switching CLI providers no longer drops you into the fallback shell - the killed process reported its exit after the replacement had started and was read as a crash. PTY processes are now generation-tagged
- Whisper load failures report the underlying error instead of a bare "Failed to load Whisper model"

### Improvements

- Terminal switched from winpty to ConPTY, restoring colour runs, wide glyphs, and the alternate screen buffer; winpty natives dropped from the published payload
- PTY children get a repaired `PATH` (inherited entries plus machine and user registry entries), so a stripped environment cannot produce a session that fails to find the CLI
- Optional CUDA backend for Whisper under Settings > Audio, guarded by a sentinel that disarms it if a load aborts
- Updated Avalonia 11.3.13 → 11.3.20, Avalonia.Controls.WebView 11.4.0 → 11.4.1, Whisper.net 1.9.0 → 1.9.1, Microsoft.Extensions.DependencyInjection 10.0.5 → 10.0.11

## [1.1.0] - 2026-04-02

### Features

- **Multi-capture mode** - Accumulate multiple screenshots (e.g., scrollable content) with `Ctrl+Shift+X`, then send all at once with `Ctrl+Shift+C`. Uses a dedicated prompt that handles overlapping regions from scrolling.
- **No-focus mode** - Toggle with `Ctrl+Shift+F` to prevent the window from stealing focus when clicked, keeping your browser or other app active. Indicator shown in the title bar.

## [1.0.5] - 2026-04-01

### Improvements

- Replaced NAudio dependency with native WASAPI loopback capture using COM interop (`LibraryImport`, `GeneratedComInterface`)
- Extracted audio conversion logic into dedicated `AudioConverter` static class
- Audio capture service simplified to orchestrate the new `WasapiLoopbackCapture` component

## [1.0.4] - 2026-04-01

### Improvements

- Terminal keyboard shortcuts: Ctrl+C copies selection (falls back to SIGINT), Ctrl+V pastes from clipboard, Ctrl+A selects all, Shift+Enter inserts a literal newline
- Audio recording status now shows real-time progress updates (saving, transcribing) in the status bar
- Recording indicator replaced with animated pulsing dot + "REC" label
- Terminal panel now has horizontal padding to prevent overlap with window resize borders

### Refactors

- Audio state management moved from callback pattern to event-based `AudioStateChanged` on `AudioInjectorService`

## [1.0.3] - 2026-04-01

### Improvements

- Audio transcripts are now saved to a `.txt` file and passed by file path to the CLI, instead of inlining the full transcript text into the terminal

## [1.0.2] - 2026-04-01

### Fixes

- Fixed version display showing 1.0.0 after update - now reads version from the main app instead of the Updater DLL
- App now checks for updates automatically on startup instead of requiring a manual check in settings
- Added Updater project to release workflow version stamping

## [1.0.1] - 2026-04-01

### Improvements

- Updated default hotkeys from `Shift+C/A/O` to `Ctrl+Shift+C/A/O` to avoid conflicts with normal typing
- Terminal `cls` command now properly clears the scrollback buffer
- Window starts centered on screen
- System tray icon with show/exit menu
- Renamed extraction directory from `stealthcode_bin` to `stealthcode_app`

### Fixes

- Fixed `ContentProtectionService` missing using directive causing Release build failure

## [1.0.0] - 2026-04-01

Initial public release.

### Features

- **Screen capture protection** - Window is invisible to screenshots, screen recordings, and screen sharing
- **Always-on-top overlay** - Pin the terminal above other windows with adjustable opacity
- **Multiple AI CLIs** - Switch between Claude Code, Codex, and Gemini CLI from the title bar
- **Built-in terminal** - Full xterm.js terminal powered by native PTY (winpty)
- **Screenshot capture** - Capture full screen, regions, or specific windows and inject into the active CLI
- **Custom system prompts** - Separate configurable prompts for screenshot and audio captures
- **Meeting audio capture** - Record system audio, transcribe locally with Whisper, and send to the CLI
- **Global hotkeys** - Configurable hotkeys for screen capture (`Ctrl+Shift+C`), audio recording (`Ctrl+Shift+A`), and opacity cycling (`Ctrl+Shift+O`)
- **System tray icon** - Minimize to tray with show/exit menu
- **Auto-updates** - Built-in GitHub release checker with one-click update
- **Self-extracting launcher** - Ships as a single portable executable, no installation needed
