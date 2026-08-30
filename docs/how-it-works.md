# How It Works

A technical overview of how Stealth Code works under the hood.

## Screen Capture Protection

Stealth Code uses the Windows [`SetWindowDisplayAffinity`](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-setwindowdisplayaffinity) API to make the window invisible to all screen capture methods - screenshots, recordings, and screen sharing.

When the window opens, the app sets the display affinity flag on the window handle:

1. **`WDA_EXCLUDEFROMCAPTURE` (0x11)** is tried first. This makes the window completely invisible to capture tools while remaining visible on the physical display. Available on Windows 10 2004+.
2. **`WDA_MONITOR` (0x01)** is the fallback for older Windows versions. It replaces the window content with a black rectangle in any capture.

This means tools like OBS, Zoom screen share, Windows Snipping Tool, and `PrintScreen` will either skip the window entirely or show a blank area. Only you can see the terminal on your monitor.

> Protection is disabled in DEBUG builds for development convenience.

## Terminal Emulator

The terminal is a full **xterm.js** instance running inside a **WebView2** (Chromium) control, connected to a real Windows PTY (pseudo-terminal).

```text
User types → xterm.js → JSON message → WebView2 bridge → C# → PTY stdin
PTY stdout → C# → base64 encode → WebView2 bridge → xterm.js renders
```

**How it connects:**

1. **PTY backend** - Uses [ConPTY](https://devblogs.microsoft.com/commandline/windows-command-line-introducing-the-windows-pseudo-console-conpty/) via Quick.PtyNet, which imports a bundled `conpty.dll` it does not ship - `ConPtyProvider` redirects those imports to `kernel32`. Replaces winpty, whose screen-scraping lost colour runs, wide glyphs, and the alternate screen buffer.
2. **WebView bridge** - A `NativeWebView` control hosts xterm.js. User keystrokes are sent as JSON messages from JavaScript to C# via `invokeCSharpAction()`. PTY output is base64-encoded and written to xterm via `InvokeScript("termWrite(...)")`.
3. **Resize sync** - xterm.js reports column/row changes via `ResizeObserver`, which propagates to the PTY so the shell reflows correctly.

The terminal supports full 256-color ANSI, cursor positioning, and alternate screen buffers - everything a modern CLI expects.

**Nothing on disk:** `TerminalAssets` inlines xterm's stylesheet and scripts from embedded resources into a single document and hands it to the WebView via `NavigateToString`, so the terminal is never unpacked to an `assets/` folder. The WebView2 host is pointed at an InPrivate profile under the temp directory that is deleted on the way out, so a session leaves behind no browsing data either.

**Locked down:** terminal output is untrusted text, so once the terminal document has loaded, `NavigationStarted` refuses every further navigation and `NewWindowRequested` swallows popups. Nothing the CLI prints can navigate the WebView away or open a browser window.

## Screenshot Capture & Injection

Stealth Code can capture your screen and inject the screenshot directly into the active CLI session for AI analysis.

**Capture modes:**

| Mode | Method |
| --- | --- |
| Full Screen | GDI `BitBlt` from the desktop DC using system metrics |
| Region | `BitBlt` with user-defined X/Y/W/H offsets |
| Window | `PrintWindow` API for the target window (falls back to `BitBlt` if it fails) |

**The capture pipeline:**

1. **GDI capture** - Creates a compatible device context and bitmap, performs the blit, and wraps it in a RAII struct (`GdiBitmap`) that auto-releases resources.
2. **PNG encoding** - A custom `PngWriter` encodes the bitmap as PNG with zero external dependencies - writes IHDR, IDAT (deflated), and IEND chunks with CRC32 checksums. BGRA pixel data from GDI is converted to RGBA in-place before encoding.
3. **Save** - The PNG is saved to `%APPDATA%/StealthCode/captures/capture_<timestamp>.png`.
4. **Inject** - The file path is sent to the PTY as a formatted prompt: the configured system prompt + the screenshot path. The CLI reads the file and responds with its analysis.

For minimized windows, the app restores them briefly via `ShowWindow(SW_RESTORE)` and waits 200ms for the window to render before capturing.

### Multi-Capture Mode

For content that doesn't fit in a single screenshot (e.g., long coding problems that require scrolling), multi-capture mode lets you accumulate multiple screenshots and send them all at once.

**The flow:**

1. Press `Ctrl+Shift+X` to take the first screenshot - the title bar shows a capture counter.
2. Scroll the content and press `Ctrl+Shift+X` again to capture the next portion. Repeat as needed.
3. Press `Ctrl+Shift+C` to finalize - all accumulated screenshots are sent to the CLI with a special prompt that instructs the AI to treat them as one continuous document and ignore overlapping regions from scrolling.

Each screenshot is saved as a separate PNG. The multi-capture system prompt is configurable independently from the single-capture prompt.

## No-Focus Mode

Stealth Code can be configured to **not steal focus** from other windows when clicked. This is useful when you need to keep a browser tab or other application active while glancing at AI responses.

**How it works:**

Toggling no-focus mode (`Ctrl+Shift+F`) adds the [`WS_EX_NOACTIVATE`](https://learn.microsoft.com/en-us/windows/win32/winmsg/extended-window-styles) extended window style to the window via `SetWindowLongPtr`. This tells Windows not to activate the window on mouse clicks - the previously focused application retains keyboard focus.

When active, a "NO-FOCUS" indicator appears in the title bar. All global hotkeys (capture, audio, opacity) continue to work since they use `RegisterHotKey` and don't require window focus. Press the hotkey again to re-enable normal focus for typing in the terminal.

## Audio Capture & Transcription

Stealth Code records system audio (what you hear through your speakers/headphones) and transcribes it using a local Whisper model - no cloud services involved.

**The audio pipeline:**

1. **WASAPI loopback** - Opens the default render endpoint in loopback mode through hand-rolled WASAPI COM interop (`GeneratedComInterface`, no NAudio dependency). This captures all audio playing on the default output device (meeting audio, YouTube, etc.).
2. **Format conversion** - Raw audio (typically 48kHz stereo float32) is converted to 16kHz mono int16 PCM, which is what Whisper expects. This involves:
   - Parsing IEEE float32 or int16 samples
   - Downmixing stereo/multichannel to mono by averaging channels
   - Resampling to 16kHz via linear interpolation
3. **Whisper transcription** - [Whisper.net](https://github.com/sandrohanea/whisper.net) (a C# wrapper around whisper.cpp) runs the `ggml-base` model locally on the CPU backend. Whisper.net's default order tries CUDA first and falls through on failure, but a failed CUDA load aborts the process rather than falling back, so `WhisperRuntime` pins the order to a single backend. CUDA is opt-in under Settings > Audio, guarded by a sentinel that disarms it if a load aborts.
4. **Inject** - The transcription text is wrapped with the configured system prompt and sent to the PTY, just like screenshot injection.

**Usage is toggle-based:** press the hotkey once to start recording, press again to stop. The transcription runs asynchronously, and results appear in the terminal once ready.

## Window Opacity

The opacity slider uses Win32 **layered window attributes** rather than Avalonia's built-in opacity (which doesn't affect the WebView2 child HWND).

1. Adds the `WS_EX_LAYERED` extended window style via `SetWindowLongPtr`
2. Calls `SetLayeredWindowAttributes` with `LWA_ALPHA` flag and a byte alpha value (0-255)

This makes the entire window - including the WebView2 terminal - uniformly transparent, so you can read code underneath while keeping the AI response visible.

## Auto-Updates

The app checks GitHub Releases for new versions and can update itself in-place.

1. **Check** - `GitHubReleaseClient` fetches the latest release from the GitHub API and compares semantic versions.
2. **Download** - If a newer version exists, the `.zip` release artifact is downloaded with chunked streaming and progress reporting.
3. **Extract** - The new `stealthcode.exe` launcher is extracted from the zip to a temporary file.
4. **Swap** - A batch script is generated that:
   - Waits for the current process to exit
   - Replaces the old launcher with the new one
   - Cleans up the old extracted binaries
   - Launches the updated app
   - Deletes itself

## Launcher (Single-File Distribution)

Stealth Code ships as a single `stealthcode.exe` - a lightweight AOT-compiled launcher with the entire app embedded as compressed resources.

**On first run:**

1. The launcher iterates its embedded manifest resources (the main app, xterm.js assets, Whisper runtime, etc.)
2. Each resource is GZip-decompressed and written to a `stealthcode_app/` subdirectory
3. The main `StealthCode.exe` is launched from the extracted directory
4. The launcher waits for the app to exit and returns its exit code

**On subsequent runs**, the launcher skips extraction (files already exist) and launches directly. Updates replace just the launcher, which re-extracts on next run.
