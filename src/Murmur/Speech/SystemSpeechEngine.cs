using System.Speech.Recognition;
using System.Text;

namespace Murmur.Speech;

/// <summary>
/// Dictation via Windows' built-in SAPI recognizer (System.Speech). This is
/// the "Primary" engine from the project brief; a secondary high-accuracy
/// engine (Vosk, if added) would implement the same ISpeechEngine interface.
///
/// SpeechRecognitionEngine reports completed phrases as the user pauses, not
/// one block at the end of an utterance, so results are accumulated in
/// _buffer across the whole hold-to-talk session and only surfaced as
/// "final" once StopListening's async stop actually completes.
/// </summary>
public sealed class SystemSpeechEngine : ISpeechEngine
{
    private readonly SpeechRecognitionEngine? _engine;
    private readonly StringBuilder _buffer = new();
    private bool _stopRequested;

    public bool IsAvailable { get; }

    public event EventHandler<string>? PartialResultRecognized;
    public event EventHandler<string>? FinalResultRecognized;

    public SystemSpeechEngine()
    {
        try
        {
            _engine = new SpeechRecognitionEngine();
            _engine.LoadGrammar(new DictationGrammar());
            _engine.SetInputToDefaultAudioDevice();

            _engine.SpeechRecognized += OnSpeechRecognized;
            _engine.RecognizeCompleted += OnRecognizeCompleted;

            IsAvailable = true;
        }
        catch (Exception)
        {
            // No recognizer/language pack installed, or no default microphone,
            // or "Let desktop apps access your microphone" is off. Any of
            // these are environment issues, not bugs: degrade quietly so the
            // caller can disable dictation UI instead of crashing on launch.
            // SpeechRecognitionEngine doesn't document a narrower exception
            // type for "recognizer not installed", hence the broad catch.
            _engine?.Dispose();
            _engine = null;
            IsAvailable = false;
        }
    }

    private void OnSpeechRecognized(object? sender, SpeechRecognizedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(e.Result?.Text))
        {
            return;
        }

        if (_buffer.Length > 0)
        {
            _buffer.Append(' ');
        }
        _buffer.Append(e.Result.Text);
        PartialResultRecognized?.Invoke(this, _buffer.ToString());
    }

    private void OnRecognizeCompleted(object? sender, RecognizeCompletedEventArgs e)
    {
        if (!_stopRequested)
        {
            return;
        }

        _stopRequested = false;
        FinalResultRecognized?.Invoke(this, _buffer.ToString());
        _buffer.Clear();
    }

    public void StartListening()
    {
        if (_engine is null)
        {
            return;
        }

        _buffer.Clear();
        _engine.RecognizeAsync(RecognizeMode.Multiple);
    }

    public void StopListening()
    {
        if (_engine is null)
        {
            return;
        }

        _stopRequested = true;
        _engine.RecognizeAsyncStop();
    }

    public void Dispose()
    {
        if (_engine is null)
        {
            return;
        }

        _engine.SpeechRecognized -= OnSpeechRecognized;
        _engine.RecognizeCompleted -= OnRecognizeCompleted;
        _engine.Dispose();
    }
}
