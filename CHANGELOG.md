# Changelog

All notable changes to Stealth Code will be documented in this file.

## [1.3.0] - 2026-09-06

### Features

- **Live listening** - audio playing on your machine is transcribed as it happens, instead of being recorded and transcribed in one pass at the end. Each utterance is sent to the CLI the moment you stop speaking
- Transcripts are typed straight into the terminal rather than saved to a file for the CLI to open
- A transcript panel below the terminal shows what has been heard so far
- Settings > Audio picks the Whisper model, the spoken language, and how long a pause ends an utterance
- Every action is now a button. Capture, multi-capture, no-focus, opacity and listening were hotkey-only, and the bottom bar just listed the hotkeys; it is now a row of buttons that do the same thing - which matters most in no-focus mode, where clicking is safe
- Settings is split into three tabs instead of one long scroll, with all five hotkeys in one place and opacity as the four presets its hotkey cycles
- The multi-capture prompt can now be edited; it was saved in settings but had nowhere to change it
- Messages appear as toasts over the terminal instead of pushing the terminal aside

### Fixes

- A screenshot of a window that had closed handed the CLI a file that was never written
- The window no longer freezes while a screenshot is taken
- Codex now receives pasted prompts - it ignored the Enter that followed a paste, leaving the prompt sitting unsent
- Stopping listening no longer leaves the old status on screen
- A hotkey another app has already claimed is now reported instead of silently doing nothing
- The PROTECTED badge shows the real state; it previously said PROTECTED whether or not protection had been enabled
- Capturing on a CLI that cannot read images now explains why, and the button is disabled with the reason in its tooltip
- Escape works as a hotkey again
- Cancelling a model download had no effect

### Improvements

- Reworked colours for readability - the status bar and title-bar chips were too faint against the background, and inputs showed a blue focus ring that clashed with the accent
- The terminal follows the app theme, instead of staying on the previous colours until restart

## [1.2.1] - 2026-08-31

### Improvements

- Shortened the GPU backend hint in Settings > Audio so it fits the panel in two lines

## [1.2.0] - 2026-08-30

### Fixes

- Whisper now works in published builds - ILC's default x64 baseline omits AVX, so `Avx.IsSupported` was always false and Whisper.net refused its CPU backend. Fixed with `IlcInstructionSet=avx`
- Whisper no longer kills the app when CUDA fails to load - a failed load aborts the process instead of falling back, so the backend is pinned to a single runtime, CPU by default
- Switching CLI providers no longer drops you into the fallback shell - the killed process reported its exit after the replacement had started and was read as a crash. PTY processes are now generation-tagged
- Whisper load failures report the underlying error instead of a bare "Failed to load Whisper model"
- CUDA now works on machines with a 12.x driver - Whisper.net 1.9.1 splits CUDA into two incompatible runtimes and the backend was pinned to the 13.x one, so 12.x machines hit the aborting load the sentinel exists to survive. Both are now offered
- Transcription errors no longer reach the CLI as if they were speech - a failure was written to the transcript file and announced as something to go and read. Transcription now returns a result the caller can tell apart from a transcript

### Improvements

- Terminal switched from winpty to ConPTY, restoring colour runs, wide glyphs, and the alternate screen buffer; winpty natives dropped from the published payload
- PTY children get a repaired `PATH` (inherited entries plus machine and user registry entries), so a stripped environment cannot produce a session that fails to find the CLI
- Published executable is 19 MB, down from 160 MB. The CUDA runtime that made up 92% of it is no longer bundled; GPU support is now a pack downloaded on demand from nuget.org, hash-checked, and stored under `%APPDATA%`
- Settings > Audio picks the Whisper backend: CPU, Vulkan (35 MB, any GPU including AMD and Intel), CUDA 13 (136 MB), or CUDA 12 (238 MB)
- Dropped ~12 MB of natives that could never load on Windows x64 - Whisper.net ships every platform's binaries, and the Linux `.so` files were being read as the Somali locale and packed into a satellite assembly
- Updated Whisper.net 1.9.0 → 1.9.1, Microsoft.Extensions.DependencyInjection 10.0.5 → 10.0.11
- Migrated to Avalonia 12.1.1 (from 11.3.13) and Avalonia.Controls.WebView 12.1.0. `Avalonia.Diagnostics` has no v12 release and is replaced by `AvaloniaUI.DiagnosticsSupport`
- Terminal assets are no longer unpacked to an `assets/` folder beside the executable - xterm's stylesheet and scripts are inlined into a single document handed to the WebView from memory
- WebView2 runs on a throwaway InPrivate profile under the temp directory, so a session leaves no browsing data behind
- Terminal output can no longer navigate the WebView away from the terminal document or open popup windows

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
