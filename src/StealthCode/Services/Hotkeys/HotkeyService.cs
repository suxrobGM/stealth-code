using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace StealthCode.Services.Hotkeys;

/// <summary>
/// A service for registering global hotkeys on Windows.
/// It allows you to specify a hotkey combination (e.g. "Ctrl+Shift+X") and a callback action that will be invoked when the hotkey is pressed,
/// even if the application is not in the foreground.
/// </summary>
public sealed partial class HotkeyService : IDisposable
{
    private IntPtr hwnd;
    private IntPtr oldWndProc;
    private bool wndProcInstalled;
    private WndProcDelegate? wndProcDelegate;

    private int nextHotkeyId = 1;
    private readonly Dictionary<string, int> namedIds = [];
    private readonly Dictionary<int, Action> callbacks = [];

    public bool Register(string name, string hotkeyString, IntPtr windowHandle, Action callback)
    {
        if (windowHandle == IntPtr.Zero)
        {
            return false;
        }

        if (namedIds.ContainsKey(name))
        {
            Unregister(name);
        }

        hwnd = windowHandle;

        var id = nextHotkeyId++;
        ParseHotkey(hotkeyString, out var modifiers, out var vk);

        if (modifiers == 0 && vk == 0)
        {
            return false;
        }

        if (!RegisterHotKey(windowHandle, id, modifiers, vk))
        {
            return false;
        }

        namedIds[name] = id;
        callbacks[id] = callback;

        if (!wndProcInstalled)
        {
            oldWndProc = GetWindowLongPtr(windowHandle, GWLP_WNDPROC);
            wndProcDelegate = WndProc;
            SetWindowLongPtr(windowHandle, GWLP_WNDPROC, Marshal.GetFunctionPointerForDelegate(wndProcDelegate));
            wndProcInstalled = true;
        }

        return true;
    }

    public void Unregister(string name)
    {
        if (!OperatingSystem.IsWindows() || !namedIds.TryGetValue(name, out var id))
        {
            return;
        }

        UnregisterHotKey(hwnd, id);
        callbacks.Remove(id);
        namedIds.Remove(name);
    }

    private void UnregisterAll()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        foreach (var id in namedIds.Values)
        {
            UnregisterHotKey(hwnd, id);
        }

        namedIds.Clear();
        callbacks.Clear();

        if (wndProcInstalled && oldWndProc != IntPtr.Zero)
        {
            SetWindowLongPtr(hwnd, GWLP_WNDPROC, oldWndProc);
            oldWndProc = IntPtr.Zero;
        }

        wndProcDelegate = null;
        wndProcInstalled = false;
    }

    private IntPtr WndProc(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam)
    {
        if (msg == WM_HOTKEY && callbacks.TryGetValue((int)wParam, out var cb))
        {
            cb.Invoke();
        }

        return CallWindowProc(oldWndProc, hWnd, msg, wParam, lParam);
    }

    private static void ParseHotkey(string hotkey, out uint modifiers, out uint vk)
    {
        modifiers = 0;
        vk = 0;

        foreach (var part in hotkey.Split('+', StringSplitOptions.TrimEntries))
        {
            var flag = HotkeyCodes.ModifierFlag(part);

            if (flag != 0)
            {
                modifiers |= flag;
            }
            else
            {
                vk = HotkeyCodes.KeyCode(part);
            }
        }
    }

    public void Dispose()
    {
        UnregisterAll();
    }

    #region Win32 API Constants and Imports

    private delegate IntPtr WndProcDelegate(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

    private const int GWLP_WNDPROC = -4;
    private const uint WM_HOTKEY = 0x0312;

    [LibraryImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

    [LibraryImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool UnregisterHotKey(IntPtr hWnd, int id);

    [LibraryImport("user32.dll", EntryPoint = "GetWindowLongPtrW")]
    private static partial IntPtr GetWindowLongPtr(IntPtr hWnd, int nIndex);

    [LibraryImport("user32.dll", EntryPoint = "SetWindowLongPtrW")]
    private static partial IntPtr SetWindowLongPtr(IntPtr hWnd, int nIndex, IntPtr dwNewLong);

    [LibraryImport("user32.dll", EntryPoint = "CallWindowProcW")]
    private static partial IntPtr CallWindowProc(IntPtr lpPrevWndFunc, IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

    #endregion
}
