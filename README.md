# Murmur

Local push-to-talk dictation for Windows 10/11 (x64). No cloud calls.

## Status

All planned pieces are now in place: app skeleton, global hotkey trigger,
Windows native speech (SAPI) wired to hold-to-talk, an optional offline Vosk
engine, clipboard+Ctrl-V text injection into the foreground app, a
persisted/browsable transcription history, and a custom dictionary for
technical terms. Built and written on a non-Windows container —
**not yet compiled or run on Windows**. Open `Murmur.sln` in Visual Studio
(or `dotnet build` / `dotnet run` from `src/Murmur`) on a Windows machine to
verify before relying on it.

Prerequisites to actually see a transcription:
- Windows Speech engine (default): a speech recognizer + language pack
  installed (Settings > Time & Language > Speech) and desktop apps allowed
  to use the microphone (Settings > Privacy > Microphone).
- Vosk engine (optional, selectable in the Engine dropdown): a Vosk model
  downloaded separately (not bundled — models run tens to hundreds of MB)
  and extracted to `%AppData%\Murmur\models\vosk-model-small-en-us-0.15`.

If either engine's prerequisites are missing, the app disables that engine
in the UI and says why instead of crashing — this still needs to be
confirmed by hand on Windows.

## Architecture decisions (and why)

Three subsystems in the original design are known AV/EDR flags or fragile
integration points. Simpler alternatives were chosen deliberately:

| Concern | Rejected | Chosen | Why |
|---|---|---|---|
| Hotkey trigger | `SetWindowsHookEx(WH_KEYBOARD_LL)` | `RegisterHotKey` + bounded `GetAsyncKeyState` poll of the *same* key | A global hook sees every keystroke on the machine — a keylogger signature. `RegisterHotKey` only ever sees the one registered combo. |
| Text injection | `SendInput` with an Electron-detection fallback | Clipboard + simulated Ctrl+V, always | One code path instead of two; works uniformly across Win32/WPF/Electron/browser targets. |
| Secondary STT engine | Parakeet TDT via ONNX Runtime | Vosk (offline, optional, user-selectable) | Skips ONNX Runtime, execution-provider tuning, and NeMo→ONNX conversion/licensing risk entirely; a real pre-packaged offline model instead. |
| Custom dictionary | Grammar-level vocabulary biasing at recognition time | Post-recognition find-and-replace of known mis-hearings | Neither SAPI's `DictationGrammar` nor Vosk's default model exposes a way to bias decoding toward specific terms; textual correction after the fact is the pragmatic alternative. |

## Current pieces

- `src/Murmur/Hotkey/NativeMethods.cs` — P/Invoke surface, `RegisterHotKey`-based only.
- `src/Murmur/Hotkey/HotkeyManager.cs` — registers Right Ctrl as push-to-talk.
  Fires `TalkStarted`/`TalkStopped`. If registration fails (combo already
  claimed, or blocked by policy), the app disables the hotkey rather than
  crashing, and says so.
- `src/Murmur/Speech/ISpeechEngine.cs` — engine abstraction so a second
  engine (e.g. Vosk) can be dropped in later without touching the UI/hotkey
  wiring.
- `src/Murmur/Speech/SystemSpeechEngine.cs` — dictation via
  `System.Speech.Recognition` (SAPI). Accumulates recognized phrases across
  a hold-to-talk session (SAPI reports completed phrases as the speaker
  pauses, not one block at the end) and surfaces the joined text as both a
  live partial and the final result. Degrades to `IsAvailable = false` if no
  recognizer/language pack or default microphone is present, rather than
  throwing on construction.
- `src/Murmur/Injection/NativeMethods.cs` — `SendInput` P/Invoke, used only
  to simulate the Ctrl+V keystroke, never to inject characters directly
  (direct injection is known to silently no-op in Electron apps).
- `src/Murmur/Injection/ClipboardInjector.cs` — saves the clipboard, sets it
  to the dictated text, simulates Ctrl+V to the foreground window, then
  restores whatever was on the clipboard before. Runs unconditionally (not
  as a SendInput fallback) so there's one code path across Win32/WPF/
  Electron/browser targets.
- `src/Murmur/History/HistoryStore.cs` — persists each finalized
  transcription to a local SQLite file at `%AppData%\Murmur\history.db`, one
  short-lived connection per read/write. Degrades to `IsAvailable = false` /
  silently-skipped writes on any failure (disk full, permissions, corrupt
  file) rather than surfacing as a dictation failure.
- `src/Murmur/HistoryWindow.xaml(.cs)` — lists all stored transcriptions,
  newest first, opened from the "History" button on the main window.
- `src/Murmur/Speech/VoskSpeechEngine.cs` — optional secondary engine.
  Captures 16kHz mono PCM via NAudio's `WaveInEvent` (Vosk itself only
  decodes frames handed to it, unlike SAPI which owns the audio device) and
  feeds it to a `VoskRecognizer`. Degrades to `IsAvailable = false` if the
  model directory is missing or fails to load.
- `src/Murmur/Dictionary/DictionaryEntry.cs`, `CustomDictionaryStore.cs` —
  each entry is a correct `Term` plus comma-separated `Aliases` (common
  mis-hearings), persisted at `%AppData%\Murmur\dictionary.db` with the same
  degrade-on-failure pattern as history.
- `src/Murmur/Dictionary/DictionaryCorrector.cs` — replaces each alias with
  its term in recognized text, whole-word and case-insensitive, longest
  alias first to avoid partial-overlap replacements. Applied to both partial
  and final results before display/injection/history.
- `src/Murmur/DictionaryWindow.xaml(.cs)` — add/list/delete dictionary
  entries, opened from the "Dictionary" button on the main window.
- `src/Murmur/MainWindow.xaml(.cs)` — window showing hotkey/speech
  availability, an Engine dropdown (Windows Speech / Vosk — Vosk only
  selectable if its model was found), live talk state, the transcript box,
  and History/Dictionary buttons. Holding Right Ctrl should start listening
  on whichever engine is selected, releasing should apply dictionary
  corrections, finalize the transcript, paste it into whatever window has
  focus, and add it to history.

## Known limitations

- Push-to-talk requires the app to run un-elevated; some corporate AV/EDR
  tools may still block `RegisterHotKey` or the polling loop outright.
- SAPI dictation accuracy is noticeably lower than cloud engines or
  Parakeet/Whisper-class models; this is the tradeoff for staying fully
  local with no ONNX integration risk (see architecture table above).
- The clipboard save/restore delays in `ClipboardInjector` (40ms before
  paste, 200ms before restore) are unvalidated guesses — need tuning against
  real target apps, especially Electron ones, which may need longer.
- If Murmur's own window has focus when dictation finishes, the paste lands
  in Murmur's transcript box — same behavior as a normal manual paste, but
  worth confirming isn't surprising in practice.
- Dictionary correction is a post-recognition text substitution, not true
  grammar-level vocabulary biasing (see architecture table) — it only fixes
  mis-hearings you've already seen and added as aliases, it can't improve
  recognition of a term the engine has never produced anything close to.
- Vosk's default English models produce lowercase, unpunctuated text; no
  punctuation restoration or capitalization pass exists yet.
- History has no delete/search/export — browse-only for now.

## Next steps (not yet done)

1. Verify everything on real Windows: hotkey fires, SAPI dictation produces
   text, Vosk loads and transcribes once a model is downloaded, injection
   lands correctly in VS Code/Cursor/Slack, dictionary corrections apply,
   and history persists across restarts. Tune the settle delays in
   `ClipboardInjector` against what's observed.
2. Consider punctuation/capitalization post-processing, especially for Vosk.
3. History delete/search/export.
