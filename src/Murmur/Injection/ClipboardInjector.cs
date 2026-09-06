using System.Windows;

namespace Murmur.Injection;

/// <summary>
/// Injects dictated text into whatever window currently has focus, via the
/// clipboard + a simulated Ctrl+V (see NativeMethods.SendCtrlV for why this
/// is the only injection path, not a fallback). The pre-existing clipboard
/// contents are saved and restored around the paste so dictation doesn't
/// clobber the user's clipboard.
///
/// Must run on the WPF UI (STA) thread — System.Windows.Clipboard requires it.
/// </summary>
public sealed class ClipboardInjector
{
    // Gives the target app's clipboard-format listeners time to see the
    // change, and time to actually read the clipboard after the paste
    // keystrokes are sent, before it's restored out from under it. Not
    // scientifically tuned; needs verification against real target apps.
    private const int PostSetClipboardDelayMs = 40;
    private const int PostPasteRestoreDelayMs = 200;

    public async Task InjectAsync(string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return;
        }

        string? previousText = TryGetClipboardText();

        Clipboard.SetText(text);
        await Task.Delay(PostSetClipboardDelayMs);

        NativeMethods.SendCtrlV();

        await Task.Delay(PostPasteRestoreDelayMs);

        RestoreClipboard(previousText);
    }

    private static string? TryGetClipboardText()
    {
        try
        {
            return Clipboard.ContainsText() ? Clipboard.GetText() : null;
        }
        catch (Exception)
        {
            // Clipboard can be transiently locked by another process. Not
            // being able to save it shouldn't block dictation from happening.
            return null;
        }
    }

    private static void RestoreClipboard(string? previousText)
    {
        try
        {
            if (previousText is not null)
            {
                Clipboard.SetText(previousText);
            }
            else
            {
                Clipboard.Clear();
            }
        }
        catch (Exception)
        {
            // Best-effort restore; leaving the dictated text on the clipboard
            // is a much smaller problem than throwing out of a background task.
        }
    }
}
