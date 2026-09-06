# How it works

## Capture protection

The main window is excluded from capture with the Windows [`SetWindowDisplayAffinity`](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-setwindowdisplayaffinity) API. On Windows 10 2004 and newer the window is absent from captures. On older versions it appears as a black box. Because the OS does the hiding, every capture tool is covered. The **PROTECTED** badge shows only when Windows accepted the request. Debug builds skip protection.

## Terminal

A real Windows pseudo-console (ConPTY) rendered by xterm.js inside WebView2. The CLI gets full colour, cursor control, and the alternate screen. The terminal page is embedded in the app, the WebView uses a throwaway profile, and further navigation and pop-ups are blocked so CLI output cannot open anything.

## Screenshots

The capture hotkey grabs the full screen, a saved region, or one window, encodes it as PNG, saves it to `%APPDATA%\StealthCode\captures`, and types your screenshot prompt plus the file path into the CLI. Minimised windows are restored for the shot. If the CLI cannot read images the button is disabled with the reason.

Multi-capture: press the multi-capture hotkey for each screen while scrolling, then the capture hotkey to send them all with a prompt that treats them as one page.

## Live listening

1. Audio from the default output device is captured in loopback mode.
2. It is converted to 16 kHz mono for Whisper.
3. [Whisper.net](https://github.com/sandrohanea/whisper.net) transcribes it locally.
4. When the speaker pauses longer than the end-of-utterance setting (1.8 s by default), the text is typed into the CLI with your audio prompt. A transcript panel shows what was heard.

Settings > Audio picks the model, language, and pause length.

### GPU packs

Only the CPU build ships. Faster backends download from nuget.org, are hash-checked, and unpack under `%APPDATA%\StealthCode\gpu`:

| Backend | Size | Works with |
| --- | --- | --- |
| Vulkan | 35 MB | most recent GPUs, including AMD and Intel |
| CUDA 13 | 136 MB | NVIDIA, 13.x driver |
| CUDA 12 | 238 MB | NVIDIA, 12.x driver or older cards |

A missing or crashing pack falls back to the CPU with a message.

## No-focus mode

Adds `WS_EX_NOACTIVATE` so clicks do not move keyboard focus to the terminal. Hotkeys keep working.

## Opacity

Applied with a Win32 layered window, because the WebView is a native child that Avalonia's opacity cannot fade.

## Updates and launcher

The app checks GitHub Releases on start and can replace itself with one click. `stealthcode.exe` is a small launcher with the app compressed inside; it unpacks to `stealthcode_app` on first run.
