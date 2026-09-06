using System.Windows;
using Murmur.History;
using Murmur.Hotkey;
using Murmur.Injection;
using Murmur.Speech;

namespace Murmur;

public partial class MainWindow : Window
{
    private HotkeyManager? _hotkeyManager;
    private ISpeechEngine? _speechEngine;
    private readonly ClipboardInjector _injector = new();
    private readonly HistoryStore _historyStore = new();

    public MainWindow()
    {
        InitializeComponent();
        Loaded += OnLoaded;
        Closed += OnClosed;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        _speechEngine = new SystemSpeechEngine();
        SpeechStatusText.Text = _speechEngine.IsAvailable
            ? "Speech: Windows dictation ready"
            : "Speech unavailable: no recognizer/language pack installed, no " +
              "default microphone, or desktop apps are blocked from the " +
              "microphone in Windows Privacy settings.";

        _hotkeyManager = new HotkeyManager(this);
        HotkeyStatusText.Text = _hotkeyManager.IsAvailable
            ? "Hotkey: Right Ctrl (hold to talk)"
            : "Hotkey unavailable: Right Ctrl may already be bound by another app, " +
              "or blocked by security policy. Push-to-talk is disabled until a " +
              "different hotkey is configured.";

        var canDictate = _hotkeyManager.IsAvailable && _speechEngine.IsAvailable;
        _hotkeyManager.TalkStarted += (_, _) =>
        {
            TalkStateText.Text = "Listening...";
            if (canDictate)
            {
                _speechEngine.StartListening();
            }
        };
        _hotkeyManager.TalkStopped += (_, _) =>
        {
            TalkStateText.Text = "Idle";
            if (canDictate)
            {
                _speechEngine.StopListening();
            }
        };

        _speechEngine.PartialResultRecognized += (_, text) =>
            Dispatcher.Invoke(() => TranscriptBox.Text = text);
        _speechEngine.FinalResultRecognized += (_, text) =>
            Dispatcher.Invoke(() =>
            {
                TranscriptBox.Text = text;
                // Fire-and-forget: injection is a background async flow (clipboard
                // set -> paste -> restore) that shouldn't block the UI thread while
                // it waits out its settle delays.
                _ = _injector.InjectAsync(text);
                _historyStore.Add(text);
            });

        TalkStateText.Text = "Idle";
    }

    private void OnHistoryButtonClick(object sender, RoutedEventArgs e)
    {
        new HistoryWindow(_historyStore) { Owner = this }.Show();
    }

    private void OnClosed(object? sender, EventArgs e)
    {
        _hotkeyManager?.Dispose();
        _speechEngine?.Dispose();
    }
}
