# Murmur

Local push-to-talk dictation for Windows 10/11 (x64). No cloud calls.

## Status

Phase 1-3 + history: app skeleton, global hotkey trigger, Windows native
speech (SAPI) wired to hold-to-talk, clipboard+Ctrl-V text injection into the
foreground app, and a persisted, browsable transcription history. Built and
written on a non-Windows container —
**not yet compiled or run on Windows**. Open `Murmur.sln` in Visual Studio
(or `dotnet build` / `dotnet run` from `src/Murmur`) on a Windows machine to
verify before relying on it.

Prerequisites to actually see a transcription: a speech recognizer +
language pack installed (Settings > Time & Language > Speech) and desktop
apps allowed to use the microphone (Settings > Privacy > Microphone). If
either is missing, the app disables dictation and says so instead of
crashing — this still needs to be confirmed by hand on Windows.

## Architecture decisions (and why)

Three subsystems in the original design are known AV/EDR flags or fragile
integration points. Simpler alternatives were chosen deliberately:

| Concern | Rejected | Chosen | Why |
|---|---|---|---|
| Hotkey trigger | `SetWindowsHookEx(WH_KEYBOARD_LL)` | `RegisterHotKey` + bounded `GetAsyncKeyState` poll of the *same* key | A global hook sees every keystroke on the machine — a keylogger signature. `RegisterHotKey` only ever sees the one registered combo. |
| Text injection | `SendInput` with an Electron-detection fallback | Clipboard + simulated Ctrl+V, always | One code path instead of two; works uniformly across Win32/WPF/Electron/browser targets. |
| Secondary STT engine | Parakeet TDT via ONNX Runtime | Windows built-in speech only, or Vosk if a second engine is wanted later | Skips ONNX Runtime, execution-provider tuning, and NeMo→ONNX conversion/licensing risk entirely. Not yet implemented. |

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
- `src/Murmur/MainWindow.xaml(.cs)` — window showing hotkey/speech
  availability, live talk state, the transcript box, and a History button,
  for manual verification. Holding Right Ctrl should start listening,
  releasing should finalize the transcript, paste it into whatever window
  has focus, and add it to history.

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
- No custom dictionary yet; history has no delete/search/export either —
  browse-only for now.

## Next steps (not yet done)

1. Verify this skeleton builds, the hotkey fires, dictation produces text,
   injection actually lands in VS Code, Cursor, and Slack, and history
   persists across restarts — all on real Windows. Tune the settle delays in
   `ClipboardInjector` against what's observed.
2. Optional Vosk secondary engine.
3. Custom dictionary for forcing correct transcription of technical terms.
