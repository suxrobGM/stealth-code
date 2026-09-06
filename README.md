<p align="center">
  <img src="src/StealthCode/Assets/logo.png" alt="Stealth Code" width="128" />
</p>

<h1 align="center">Stealth Code</h1>

<p align="center">
  Run AI coding assistants in a screen-capture-proof overlay terminal.
  <br />
  <strong>Invisible to screenshots, screen recordings, and screen sharing.</strong>
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

## Why Stealth Code?

Need to use AI coding tools during a screen share, interview prep, or recording session? Stealth Code gives you a full terminal that's **completely invisible** to any screen capture software - your secret coding companion that only you can see.

## Features

- **Screen capture protection** - Invisible to screenshots, screen recordings, and screen sharing
- **Always-on-top overlay** - Pin the terminal above other windows with adjustable opacity
- **Multiple AI CLIs** - Switch between Claude Code, Codex, and Gemini CLI from the title bar
- **Screenshot capture** - Capture your screen and inject it into the active CLI for instant AI analysis
- **Multi-capture mode** - Accumulate multiple screenshots (e.g., scrollable content) and send them all at once with overlap-aware prompting
- **No-focus mode** - Keep your browser focused while interacting with Stealth Code — clicks won't steal focus
- **Hands-free meeting audio** - Toggle a hotkey to listen; speech is transcribed locally with Whisper and sent to the CLI automatically after a short pause
- **Custom system prompts** - Set separate prompts for screenshot and audio captures to tailor AI responses (e.g., "solve in Python", "give direct answers")
- **Configurable hotkeys** - Global hotkeys for all actions, customizable in settings
- **Auto-updates** - Built-in update checker with one-click install
- **Lightweight** - Ships as a single portable executable, no installation needed

## Screenshots

<table>
  <tr>
    <td align="center"><strong>Screenshot Capture</strong></td>
    <td align="center"><strong>Audio Capture</strong></td>
  </tr>
  <tr>
    <td><img src="docs/images/screenshot-capture.png" alt="Screenshot capture" width="400" /></td>
    <td><img src="docs/images/audio-capture.png" alt="Audio capture" width="400" /></td>
  </tr>
  <tr>
    <td align="center"><em>Press <code>Ctrl+Shift+C</code> to capture your screen and inject it into the CLI. The AI sees the screenshot and responds with a solution - no copy-pasting needed.</em></td>
    <td align="center"><em>Press <code>Ctrl+Shift+A</code> to start listening. Questions are transcribed locally via Whisper and sent to the CLI automatically after you finish speaking.</em></td>
  </tr>
  <tr>
    <td align="center" colspan="2"><strong>Settings Panel</strong></td>
  </tr>
  <tr>
    <td align="center" colspan="2"><img src="docs/images/settings.png" alt="Settings" width="600" /></td>
  </tr>
  <tr>
    <td align="center" colspan="2"><em>Configure capture mode, hotkeys, AI prompts, opacity, and audio model - all in one panel.</em></td>
  </tr>
</table>

## Getting Started

1. Download the latest release from the [Releases](https://github.com/suxrobGM/stealth-code/releases) page
2. Run `stealthcode.exe` - no installation needed
3. Make sure you have at least one supported CLI tool installed:
   - [Claude Code](https://docs.anthropic.com/en/docs/claude-code)
   - [Codex](https://github.com/openai/codex)
   - [Gemini CLI](https://github.com/google-gemini/gemini-cli)

> For building from source, see [Architecture](docs/architecture.md#build--run).

## Keyboard Shortcuts

| Action | Default Shortcut |
| --- | --- |
| Capture screenshot | `Ctrl+Shift+C` |
| Multi-capture (accumulate screenshots) | `Ctrl+Shift+X` |
| Toggle audio listening | `Ctrl+Shift+A` |
| Cycle opacity | `Ctrl+Shift+O` |
| Toggle no-focus mode | `Ctrl+Shift+F` |

All hotkeys are customizable in the settings panel.

## Documentation

- [How It Works](docs/how-it-works.md) - screen protection, terminal emulator, capture pipeline, and more
- [Architecture](docs/architecture.md) - project structure, conventions, and build details
- [Changelog](CHANGELOG.md) - release history and notable changes

## License

[MIT](LICENSE) - Sukhrob Ilyosbekov
