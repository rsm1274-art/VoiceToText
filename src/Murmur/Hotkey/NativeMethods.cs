using System.Runtime.InteropServices;

namespace Murmur.Hotkey;

/// <summary>
/// P/Invoke surface for RegisterHotKey-based capture. Deliberately does NOT
/// use SetWindowsHookEx(WH_KEYBOARD_LL): a system-wide keyboard hook sees
/// every keystroke on the machine and is a well-known AV/EDR heuristic
/// trigger. RegisterHotKey only delivers WM_HOTKEY for the exact combo we
/// register, so there is nothing here that resembles a keylogger.
/// </summary>
internal static class NativeMethods
{
    [DllImport("user32.dll", SetLastError = true)]
    public static extern bool RegisterHotKey(nint hWnd, int id, uint fsModifiers, uint vk);

    [DllImport("user32.dll", SetLastError = true)]
    public static extern bool UnregisterHotKey(nint hWnd, int id);

    /// <summary>
    /// Used only to poll the single already-registered hotkey's up/down state
    /// (to support hold-to-talk instead of press-to-toggle). Never polled for
    /// arbitrary keys — that would reintroduce the same AV-heuristic problem
    /// a global hook has.
    /// </summary>
    [DllImport("user32.dll")]
    public static extern short GetAsyncKeyState(int vKey);

    public const int WM_HOTKEY = 0x0312;

    public const uint MOD_ALT = 0x0001;
    public const uint MOD_CONTROL = 0x0002;
    public const uint MOD_SHIFT = 0x0004;
    public const uint MOD_WIN = 0x0008;
    public const uint MOD_NOREPEAT = 0x4000;

    public const int VK_RCONTROL = 0xA3;
}
