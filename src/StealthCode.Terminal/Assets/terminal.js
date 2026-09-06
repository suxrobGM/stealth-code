// Injected by TerminalAssets; the ANSI palette below has no counterpart in the app theme.
const chrome = window.__terminalTheme || {};
const background = chrome.background || "#1a1a1a";
const foreground = chrome.foreground || "#d4d4d4";

document.body.style.background = background;

const toastColors = {
  info: chrome.accent || "#10b981",
  success: chrome.accent || "#10b981",
  warning: chrome.warning || "#f59e0b",
  error: chrome.danger || "#ef4444",
};

const term = new Terminal({
  theme: {
    background: background,
    foreground: foreground,
    cursor: foreground,
    cursorAccent: background,
    selectionBackground: "#264f78",
    black: "#1e1e1e",
    red: "#e87a35",
    green: "#6a9955",
    yellow: "#d7ba7d",
    blue: "#569cd6",
    magenta: "#c586c0",
    cyan: "#4ec9b0",
    white: foreground,
    brightBlack: "#808080",
    brightRed: "#f0964a",
    brightGreen: "#6a9955",
    brightYellow: "#d7ba7d",
    brightBlue: "#569cd6",
    brightMagenta: "#c586c0",
    brightCyan: "#4ec9b0",
    brightWhite: "#e5e5e5",
  },
  fontFamily:
    "'Cascadia Code', 'Cascadia Mono', 'SF Mono', 'JetBrains Mono', Consolas, monospace",
  fontSize: 14,
  lineHeight: 1.2,
  cursorBlink: true,
  cursorStyle: "bar",
  allowProposedApi: true,
});

// Load the FitAddon to enable dynamic resizing of the terminal
const fitAddon = new FitAddon.FitAddon();
term.loadAddon(fitAddon);
term.open(document.getElementById("terminal"));
fitAddon.fit();

const resizeObserver = new ResizeObserver(() => {
  fitAddon.fit();
});
resizeObserver.observe(document.getElementById("terminal"));

/**
 * Handles custom key events for the terminal.
 * Intercepts Shift+Enter, Ctrl+C (copy), and Ctrl+V (secure paste).
 * @param {KeyboardEvent} e
 * @returns {boolean} - false to prevent xterm default handling
 */
function onCustomKeyEvent(e) {
  if (e.type !== "keydown") return true;

  // Shift+Enter: newline via bracketed paste so the PTY treats it as literal text
  if (e.key === "Enter" && e.shiftKey) {
    sendMessage({ type: "input", data: "\x1b[200~\n\x1b[201~" });
    return false;
  }

  // Ctrl+C: copy selection if any, otherwise send SIGINT
  if (e.key === "c" && e.ctrlKey && !e.shiftKey && !e.altKey) {
    const selection = term.getSelection();
    if (selection) {
      navigator.clipboard.writeText(selection);
      term.clearSelection();
      return false;
    }
    return true;
  }

  // Ctrl+A: select all terminal content
  if (e.key === "a" && e.ctrlKey && !e.shiftKey && !e.altKey) {
    term.selectAll();
    return false;
  }

  // Ctrl+V: secure paste: read clipboard, write to PTY, then scrub
  if (e.key === "v" && e.ctrlKey && !e.shiftKey && !e.altKey) {
    navigator.clipboard.readText().then((text) => {
      if (text) {
        sendMessage({ type: "input", data: text });
        navigator.clipboard.writeText("");
      }
    });
    return false;
  }

  return true;
}

/**
 * Forwards terminal input data to the PTY via C# bridge.
 * @param {string} data - The input data from xterm.js
 */
function onTermData(data) {
  sendMessage({ type: "input", data: data });
}

/**
 * Notifies the C# side when the terminal dimensions change.
 * @param {{ cols: number, rows: number }} size
 */
function onTermResize(size) {
  sendMessage({ type: "resize", cols: size.cols, rows: size.rows });
}

/**
 * Widens a latin1 binary string into bytes.
 * @param {string} binary
 * @returns {Uint8Array}
 */
function toBytes(binary) {
  const bytes = new Uint8Array(binary.length);
  for (let i = 0; i < binary.length; i++) {
    bytes[i] = binary.charCodeAt(i);
  }
  return bytes;
}

/**
 * Writes base64-encoded data to the terminal.
 * @param {string} base64Data
 * @returns {void}
 */
function termWrite(base64Data) {
  const binary = atob(base64Data);
  // Detect clear-screen sequence (ESC[2J) and also clear scrollback
  if (binary.includes("\x1b[2J")) {
    term.clear();
  }
  term.write(toBytes(binary));
}

/**
 * Resizes the terminal to the specified number of columns and rows.
 * @param {number} cols
 * @param {number} rows
 * @returns {void}
 */
function termResize(cols, rows) {
  term.resize(cols, rows);
}

/**
 * Clears the terminal and resets it to its initial state.
 * @returns {void}
 */
function termReset() {
  term.reset();
}

let toastTimer = null;

/**
 * Shows one transient notice over the terminal. Base64 because the text is untrusted.
 * @param {string} base64Json - base64 of {"text": string, "level": string, "durationMs": number}
 * @returns {void}
 */
function termToast(base64Json) {
  const host = document.getElementById("toasts");
  if (!host) return;

  const payload = JSON.parse(new TextDecoder().decode(toBytes(atob(base64Json))));

  // textContent, not innerHTML: the text can come from a CLI error.
  host.textContent = payload.text;
  host.style.background = chrome.panel || "#252525";
  host.style.borderColor = chrome.border || "#333333";
  host.style.color = toastColors[payload.level] || toastColors.info;
  host.classList.add("visible");

  clearTimeout(toastTimer);
  toastTimer = setTimeout(() => host.classList.remove("visible"), payload.durationMs || 2600);
}

/**
 * Sends a message to the C# code. The message will be serialized as JSON before being sent.
 * @param {Record<string, unknown>} msg - The message to send. Must be serializable as JSON.
 * @returns {void}
 */
function sendMessage(msg) {
  if (typeof invokeCSharpAction === "function") {
    invokeCSharpAction(JSON.stringify(msg));
  }
}

term.attachCustomKeyEventHandler(onCustomKeyEvent);
term.onData(onTermData);
term.onResize(onTermResize);

sendMessage({ type: "ready", cols: term.cols, rows: term.rows });
