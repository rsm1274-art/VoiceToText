using System.Text;
using System.Text.Json;
using NAudio.Wave;
using Vosk;

namespace Murmur.Speech;

/// <summary>
/// Optional secondary dictation engine using Vosk (see README's architecture
/// table for why Vosk was chosen over Parakeet/ONNX: a real pre-packaged
/// offline model, no NeMo->ONNX conversion step, no DirectML/CPU
/// execution-provider tuning).
///
/// The model directory is NOT bundled in this repo - it's tens to hundreds
/// of MB - and must be downloaded separately and placed at the given path
/// (default: %AppData%\Murmur\models\vosk-model-small-en-us-0.15). If it's
/// missing or fails to load, this engine reports IsAvailable = false rather
/// than throwing, so the caller can fall back to SystemSpeechEngine.
///
/// Unlike SystemSpeechEngine, which owns its own audio device via SAPI,
/// Vosk only decodes PCM frames handed to it: audio capture here is done
/// with NAudio's WaveInEvent at 16kHz mono, the sample rate Vosk's English
/// models expect.
/// </summary>
public sealed class VoskSpeechEngine : ISpeechEngine
{
    private const int SampleRateHz = 16000;

    private readonly Model? _model;
    private readonly StringBuilder _buffer = new();
    private WaveInEvent? _waveIn;
    private VoskRecognizer? _recognizer;

    public bool IsAvailable { get; }

    public event EventHandler<string>? PartialResultRecognized;
    public event EventHandler<string>? FinalResultRecognized;

    public VoskSpeechEngine(string modelPath)
    {
        if (!Directory.Exists(modelPath))
        {
            IsAvailable = false;
            return;
        }

        try
        {
            Vosk.Vosk.SetLogLevel(-1);
            _model = new Model(modelPath);
            IsAvailable = true;
        }
        catch (Exception)
        {
            // Corrupt/incompatible model directory, native library load
            // failure, etc. Same "degrade, don't crash" contract as the
            // other engines.
            _model = null;
            IsAvailable = false;
        }
    }

    public void StartListening()
    {
        if (_model is null)
        {
            return;
        }

        _buffer.Clear();
        _recognizer = new VoskRecognizer(_model, SampleRateHz);

        _waveIn = new WaveInEvent { WaveFormat = new WaveFormat(SampleRateHz, 16, 1) };
        _waveIn.DataAvailable += OnDataAvailable;
        _waveIn.StartRecording();
    }

    private void OnDataAvailable(object? sender, WaveInEventArgs e)
    {
        if (_recognizer is null || e.BytesRecorded == 0)
        {
            return;
        }

        if (_recognizer.AcceptWaveform(e.Buffer, e.BytesRecorded))
        {
            AppendIfPresent(ExtractField(_recognizer.Result(), "text"));
        }
        else
        {
            var partial = ExtractField(_recognizer.PartialResult(), "partial");
            if (!string.IsNullOrWhiteSpace(partial))
            {
                var preview = _buffer.Length > 0 ? $"{_buffer} {partial}" : partial;
                PartialResultRecognized?.Invoke(this, preview);
            }
        }
    }

    public void StopListening()
    {
        if (_waveIn is null || _recognizer is null)
        {
            return;
        }

        _waveIn.DataAvailable -= OnDataAvailable;
        _waveIn.StopRecording();
        _waveIn.Dispose();
        _waveIn = null;

        AppendIfPresent(ExtractField(_recognizer.FinalResult(), "text"));

        FinalResultRecognized?.Invoke(this, _buffer.ToString());
        _buffer.Clear();

        _recognizer.Dispose();
        _recognizer = null;
    }

    private void AppendIfPresent(string? phrase)
    {
        if (string.IsNullOrWhiteSpace(phrase))
        {
            return;
        }

        if (_buffer.Length > 0)
        {
            _buffer.Append(' ');
        }
        _buffer.Append(phrase);
    }

    private static string? ExtractField(string json, string field)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            return doc.RootElement.TryGetProperty(field, out var value) ? value.GetString() : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    public void Dispose()
    {
        _waveIn?.Dispose();
        _recognizer?.Dispose();
        _model?.Dispose();
    }
}
