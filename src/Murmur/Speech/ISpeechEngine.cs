namespace Murmur.Speech;

/// <summary>
/// Abstraction over a dictation engine so a second engine (e.g. Vosk) can be
/// added later without touching the hotkey/UI wiring. Phase 2 ships exactly
/// one implementation: SystemSpeechEngine (Windows SAPI).
/// </summary>
public interface ISpeechEngine : IDisposable
{
    /// <summary>False if the engine could not initialize (no recognizer/language
    /// pack installed, or no default microphone). Callers must check this and
    /// disable dictation UI rather than call StartListening blindly.</summary>
    bool IsAvailable { get; }

    /// <summary>Raised as speech is recognized mid-utterance, with the
    /// accumulated text so far for the current listening session.</summary>
    event EventHandler<string>? PartialResultRecognized;

    /// <summary>Raised once after StopListening, with the full accumulated
    /// text for the session that just ended.</summary>
    event EventHandler<string>? FinalResultRecognized;

    void StartListening();
    void StopListening();
}
