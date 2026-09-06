using System.Windows;
using System.Windows.Interop;
using System.Windows.Threading;

namespace Murmur.Hotkey;

/// <summary>
/// Push-to-talk trigger built on RegisterHotKey rather than a low-level
/// keyboard hook (see NativeMethods for why). RegisterHotKey only tells us
/// "the combo was pressed", not "how long it's held", so hold-to-talk is
/// reconstructed by polling GetAsyncKeyState on the same key at a modest
/// interval once we know it's down. If registration fails (combo already
/// claimed by another app, or blocked by policy), the app must keep working
/// with the hotkey simply disabled rather than crashing.
/// </summary>
public sealed class HotkeyManager : IDisposable
{
    private const int HotkeyId = 0xB00; // arbitrary, process-unique id
    private const int PollIntervalMs = 40;

    private readonly HwndSource _hwndSource;
    private readonly DispatcherTimer _holdPollTimer;
    private readonly int _vk;
    private bool _isRegistered;
    private bool _isHeld;

    public event EventHandler? TalkStarted;
    public event EventHandler? TalkStopped;

    /// <summary>True if RegisterHotKey succeeded. False means the hotkey is
    /// inert and the caller should surface this in the UI rather than pretend
    /// push-to-talk is active.</summary>
    public bool IsAvailable => _isRegistered;

    public HotkeyManager(Window ownerWindow, int vk = NativeMethods.VK_RCONTROL, uint modifiers = 0)
    {
        _vk = vk;

        var helper = new WindowInteropHelper(ownerWindow);
        // Force the window handle to exist before we try to register against it.
        helper.EnsureHandle();
        _hwndSource = HwndSource.FromHwnd(helper.Handle)
            ?? throw new InvalidOperationException("Owner window has no HWND yet.");
        _hwndSource.AddHook(WndProc);

        _isRegistered = NativeMethods.RegisterHotKey(
            helper.Handle, HotkeyId, modifiers | NativeMethods.MOD_NOREPEAT, (uint)vk);

        _holdPollTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(PollIntervalMs)
        };
        _holdPollTimer.Tick += (_, _) => PollForRelease();
    }

    private nint WndProc(nint hwnd, int msg, nint wParam, nint lParam, ref bool handled)
    {
        if (msg == NativeMethods.WM_HOTKEY && wParam.ToInt32() == HotkeyId)
        {
            handled = true;
            if (!_isHeld)
            {
                _isHeld = true;
                TalkStarted?.Invoke(this, EventArgs.Empty);
                _holdPollTimer.Start();
            }
        }

        return nint.Zero;
    }

    private void PollForRelease()
    {
        // High bit set means the key is currently down.
        var isDown = (NativeMethods.GetAsyncKeyState(_vk) & 0x8000) != 0;
        if (isDown || !_isHeld)
        {
            return;
        }

        _isHeld = false;
        _holdPollTimer.Stop();
        TalkStopped?.Invoke(this, EventArgs.Empty);
    }

    public void Dispose()
    {
        _holdPollTimer.Stop();
        _hwndSource.RemoveHook(WndProc);
        if (_isRegistered)
        {
            NativeMethods.UnregisterHotKey(_hwndSource.Handle, HotkeyId);
            _isRegistered = false;
        }
    }
}
