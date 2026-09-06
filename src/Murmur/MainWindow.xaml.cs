using System.Windows;
using System.Windows.Controls;
using Murmur.Dictionary;
using Murmur.History;
using Murmur.Hotkey;
using Murmur.Injection;
using Murmur.Speech;

namespace Murmur;

public partial class MainWindow : Window
{
    private HotkeyManager? _hotkeyManager;
    private ISpeechEngine? _systemSpeechEngine;
    private ISpeechEngine? _voskSpeechEngine;
    private ISpeechEngine? _activeSpeechEngine;
    private readonly ClipboardInjector _injector = new();
    private readonly HistoryStore _historyStore = new();
    private readonly CustomDictionaryStore _dictionaryStore = new();

    public MainWindow()
    {
        InitializeComponent();
        Loaded += OnLoaded;
        Closed += OnClosed;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        _systemSpeechEngine = new SystemSpeechEngine();
        AttachEngineEvents(_systemSpeechEngine);

        var voskModelPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "Murmur", "models", "vosk-model-small-en-us-0.15");
        _voskSpeechEngine = new VoskSpeechEngine(voskModelPath);
        AttachEngineEvents(_voskSpeechEngine);

        EngineSelector.Items.Add("Windows Speech (built-in)");
        EngineSelector.Items.Add(_voskSpeechEngine.IsAvailable
            ? "Vosk (offline, optional)"
            : "Vosk (offline, optional) - model not found");
        EngineSelector.SelectedIndex = 0;
        _activeSpeechEngine = _systemSpeechEngine;
        UpdateSpeechStatusText();

        _hotkeyManager = new HotkeyManager(this);
        HotkeyStatusText.Text = _hotkeyManager.IsAvailable
            ? "Hotkey: Right Ctrl (hold to talk)"
            : "Hotkey unavailable: Right Ctrl may already be bound by another app, " +
              "or blocked by security policy. Push-to-talk is disabled until a " +
              "different hotkey is configured.";

        _hotkeyManager.TalkStarted += (_, _) =>
        {
            TalkStateText.Text = "Listening...";
            if (_activeSpeechEngine?.IsAvailable == true)
            {
                _activeSpeechEngine.StartListening();
            }
        };
        _hotkeyManager.TalkStopped += (_, _) =>
        {
            TalkStateText.Text = "Idle";
            if (_activeSpeechEngine?.IsAvailable == true)
            {
                _activeSpeechEngine.StopListening();
            }
        };

        TalkStateText.Text = "Idle";
    }

    // Both engines get their events attached up front. This is safe without
    // per-engine subscribe/unsubscribe on switch because only the currently
    // active engine ever has StartListening called on it, so the inactive
    // one never raises anything.
    private void AttachEngineEvents(ISpeechEngine engine)
    {
        engine.PartialResultRecognized += (_, text) =>
            Dispatcher.Invoke(() => TranscriptBox.Text = Correct(text));
        engine.FinalResultRecognized += (_, text) =>
            Dispatcher.Invoke(() =>
            {
                var corrected = Correct(text);
                TranscriptBox.Text = corrected;
                // Fire-and-forget: injection is a background async flow (clipboard
                // set -> paste -> restore) that shouldn't block the UI thread while
                // it waits out its settle delays.
                _ = _injector.InjectAsync(corrected);
                _historyStore.Add(corrected);
            });
    }

    private string Correct(string text) => DictionaryCorrector.Apply(text, _dictionaryStore.GetAll());

    private void OnEngineSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        _activeSpeechEngine = EngineSelector.SelectedIndex == 1 && _voskSpeechEngine?.IsAvailable == true
            ? _voskSpeechEngine
            : _systemSpeechEngine;
        UpdateSpeechStatusText();
    }

    private void UpdateSpeechStatusText()
    {
        SpeechStatusText.Text = _activeSpeechEngine?.IsAvailable == true
            ? "Speech: ready"
            : "Speech unavailable for the selected engine: no recognizer/language pack, " +
              "no default microphone, missing Vosk model, or desktop apps are blocked " +
              "from the microphone in Windows Privacy settings.";
    }

    private void OnHistoryButtonClick(object sender, RoutedEventArgs e)
    {
        new HistoryWindow(_historyStore) { Owner = this }.Show();
    }

    private void OnDictionaryButtonClick(object sender, RoutedEventArgs e)
    {
        new DictionaryWindow(_dictionaryStore) { Owner = this }.Show();
    }

    private void OnClosed(object? sender, EventArgs e)
    {
        _hotkeyManager?.Dispose();
        _systemSpeechEngine?.Dispose();
        _voskSpeechEngine?.Dispose();
    }
}
