<p align="center">
  <img src="src/StealthCode/Assets/logo.png" alt="Stealth Code" width="128" />
</p>

<h1 align="center">Stealth Code</h1>

<p align="center">
  AI assistant for live technical interviews, in a window only you can see.
  <br />
  <strong>Hears the question, sees the screen, answers in your terminal. Invisible to screen sharing.</strong>
</p>

<p align="center">
  <a href="https://github.com/suxrobGM/stealth-code/actions/workflows/build.yml">
    <img src="https://github.com/suxrobGM/stealth-code/actions/workflows/build.yml/badge.svg" alt="Build" />
  </a>
  <a href="https://github.com/suxrobGM/stealth-code/releases">
    <img src="https://img.shields.io/github/v/release/suxrobGM/stealth-code?include_prereleases&label=download" alt="Download" />
  </a>
  <a href="LICENSE">
    <img src="https://img.shields.io/github/license/suxrobGM/stealth-code" alt="License" />
  </a>
  <br />
  <img src="https://img.shields.io/badge/.NET-10-512bd4?logo=dotnet" alt=".NET 10" />
  <img src="https://img.shields.io/badge/Avalonia-12.1-8b44ac?logo=data:image/svg+xml;base64,PHN2ZyB4bWxucz0iaHR0cDovL3d3dy53My5vcmcvMjAwMC9zdmciIHZpZXdCb3g9IjAgMCAyNCAyNCI+PHBhdGggZD0iTTEyIDJMMiAyMmgyMEwxMiAyeiIgZmlsbD0id2hpdGUiLz48L3N2Zz4=" alt="Avalonia 12.1" />
  <img src="https://img.shields.io/badge/platform-Windows-0078d4?logo=windows" alt="Windows" />
</p>

<p align="center">
  <img src="docs/images/demo.gif" alt="Demo" width="800" />
</p>

---

Stealth Code runs Claude Code, Codex, or Gemini CLI in a floating terminal that Zoom, Teams, Meet, OBS, and screenshots cannot capture. During an interview it transcribes the interviewer's voice locally with Whisper and sends each question to the CLI as they finish speaking. One hotkey sends a screenshot of the coding problem. Everything runs on your machine.

## Features

- **Invisible to capture.** Excluded from screen sharing, recording, and screenshots by Windows itself.
- **Live listening.** System audio transcribed locally, each question sent to the CLI after a short pause.
- **Screenshot to answer.** Full screen, region, or window, sent with a prompt asking for the solution.
- **Multi-capture.** Screenshot while scrolling, send as one page.
- **No-focus mode.** Click the answer without taking focus from your editor.
- **Always on top, adjustable opacity.**
- **Claude Code, Codex, Gemini CLI**, or a custom command.
- **Editable prompts and hotkeys.** Portable single executable with built-in updates.

## Screenshots

<table>
  <tr>
    <td><img src="docs/images/screenshot-capture.png" alt="Screenshot capture" width="400" /></td>
    <td><img src="docs/images/audio-capture.png" alt="Audio capture" width="400" /></td>
  </tr>
  <tr>
    <td align="center"><em><code>Ctrl+Shift+C</code> sends the problem on screen to the CLI.</em></td>
    <td align="center"><em><code>Ctrl+Shift+A</code> starts listening. Questions are transcribed and answered live.</em></td>
  </tr>
  <tr>
    <td align="center" colspan="2"><img src="docs/images/settings.png" alt="Settings" width="600" /></td>
  </tr>
</table>

## Getting started

1. Install and sign in to [Claude Code](https://docs.anthropic.com/en/docs/claude-code), [Codex](https://github.com/openai/codex), or [Gemini CLI](https://github.com/google-gemini/gemini-cli).
2. Download `stealthcode.exe` from [Releases](https://github.com/suxrobGM/stealth-code/releases) and run it.
3. Pick your CLI in the title bar. Press `Ctrl+Shift+A` once to download the Whisper model.

Before a real call, share your screen in a test meeting and check the terminal is not in the preview. If the **PROTECTED** badge is missing, do not share.

## In an interview

1. Start listening (`Ctrl+Shift+A`) and turn on no-focus mode (`Ctrl+Shift+F`).
2. Spoken questions arrive in the CLI when the interviewer pauses.
3. For a pasted problem, press `Ctrl+Shift+C`. For a long one, `Ctrl+Shift+X` per screen, then `Ctrl+Shift+C`.

Use headphones so only the interviewer is transcribed. Tune the pause length, language, and prompts in Settings.

## Shortcuts

| Action | Default |
| --- | --- |
| Capture and send screenshot | `Ctrl+Shift+C` |
| Add to multi-capture | `Ctrl+Shift+X` |
| Start or stop listening | `Ctrl+Shift+A` |
| Cycle opacity | `Ctrl+Shift+O` |
| Toggle no-focus mode | `Ctrl+Shift+F` |

## Requirements

- Windows 10 2004 or newer. Older versions show a black box instead of hiding the window.
- [WebView2 Runtime](https://developer.microsoft.com/microsoft-edge/webview2/).
- A supported CLI on your `PATH`.

Data lives in `%APPDATA%\StealthCode`. Protection is off in debug builds.

## Docs

- [How it works](docs/how-it-works.md)
- [Development](docs/development.md)
- [Changelog](CHANGELOG.md)

## License

[MIT](LICENSE). Sukhrob Ilyosbekov.
