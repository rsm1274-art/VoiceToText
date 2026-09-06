using System.Windows;
using Murmur.Hotkey;

namespace Murmur;

public partial class MainWindow : Window
{
    private HotkeyManager? _hotkeyManager;

    public MainWindow()
    {
        InitializeComponent();
        Loaded += OnLoaded;
        Closed += OnClosed;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        _hotkeyManager = new HotkeyManager(this);

        HotkeyStatusText.Text = _hotkeyManager.IsAvailable
            ? "Hotkey: Right Ctrl (hold to talk)"
            : "Hotkey unavailable: Right Ctrl may already be bound by another app, " +
              "or blocked by security policy. Push-to-talk is disabled until a " +
              "different hotkey is configured.";

        _hotkeyManager.TalkStarted += (_, _) => TalkStateText.Text = "Listening...";
        _hotkeyManager.TalkStopped += (_, _) => TalkStateText.Text = "Idle";
        TalkStateText.Text = "Idle";
    }

    private void OnClosed(object? sender, EventArgs e)
    {
        _hotkeyManager?.Dispose();
    }
}
