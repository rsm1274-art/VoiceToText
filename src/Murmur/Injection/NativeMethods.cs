using System.Runtime.InteropServices;

namespace Murmur.Injection;

internal static class NativeMethods
{
    private const int InputKeyboard = 1;
    private const uint KeyeventfKeyup = 0x0002;
    private const ushort VkControl = 0x11;
    private const ushort VkV = 0x56;

    [StructLayout(LayoutKind.Sequential)]
    private struct KeybdInput
    {
        public ushort wVk;
        public ushort wScan;
        public uint dwFlags;
        public uint time;
        public nint dwExtraInfo;
    }

    [StructLayout(LayoutKind.Explicit)]
    private struct InputUnion
    {
        [FieldOffset(0)] public KeybdInput Ki;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct Input
    {
        public int Type;
        public InputUnion U;
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint SendInput(uint nInputs, Input[] pInputs, int cbSize);

    /// <summary>
    /// Simulates Ctrl+V to whatever window currently has foreground focus.
    /// Chosen over injecting the dictated characters directly (SendInput of
    /// each char, or WM_CHAR) because those are known to silently no-op in
    /// Electron apps (VS Code, Slack, Cursor). Clipboard + simulated paste is
    /// the one path that behaves the same across Win32, WPF, Electron, and
    /// browser-hosted editors, so it's used unconditionally rather than as a
    /// fallback after a direct-injection attempt.
    /// </summary>
    public static void SendCtrlV()
    {
        var inputs = new[]
        {
            KeyInput(VkControl, keyUp: false),
            KeyInput(VkV, keyUp: false),
            KeyInput(VkV, keyUp: true),
            KeyInput(VkControl, keyUp: true),
        };

        SendInput((uint)inputs.Length, inputs, Marshal.SizeOf<Input>());
    }

    private static Input KeyInput(ushort vk, bool keyUp) => new()
    {
        Type = InputKeyboard,
        U = new InputUnion
        {
            Ki = new KeybdInput
            {
                wVk = vk,
                wScan = 0,
                dwFlags = keyUp ? KeyeventfKeyup : 0,
                time = 0,
                dwExtraInfo = nint.Zero,
            },
        },
    };
}
