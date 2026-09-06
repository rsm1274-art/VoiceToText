# Murmur

Local push-to-talk dictation for Windows 10/11 (x64). No cloud calls.

## Status

Phase 1 + 2: app skeleton, global hotkey trigger, and Windows native speech
(SAPI) wired to hold-to-talk. Built and written on a non-Windows container —
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
| Text injection | `SendInput` with an Electron-detection fallback | Clipboard + simulated Ctrl+V, always | One code path instead of two; works uniformly across Win32/WPF/Electron/browser targets. Not yet implemented (phase 3). |
| Secondary STT engine | Parakeet TDT via ONNX Runtime | Windows built-in speech only, or Vosk if a second engine is wanted later | Skips ONNX Runtime, execution-provider tuning, and NeMo→ONNX conversion/licensing risk entirely. Not yet implemented (phase 2+). |

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
- `src/Murmur/MainWindow.xaml(.cs)` — window showing hotkey/speech
  availability, live talk state, and the transcript box, for manual
  verification. Holding Right Ctrl should start listening and fill the
  transcript box; releasing should finalize it.

## Known limitations

- Push-to-talk requires the app to run un-elevated; some corporate AV/EDR
  tools may still block `RegisterHotKey` or the polling loop outright.
- SAPI dictation accuracy is noticeably lower than cloud engines or
  Parakeet/Whisper-class models; this is the tradeoff for staying fully
  local with no ONNX integration risk (see architecture table above).
- No text injection, history, or dictionary yet — the transcript only
  appears in Murmur's own window so far.

## Next steps (not yet done)

1. Verify this skeleton builds, the hotkey fires, and dictation actually
   produces text on real Windows.
2. Clipboard+Ctrl-V text injection into the foreground app, tested against
   VS Code/Cursor/Slack.
3. Optional Vosk secondary engine.
4. History window (SQLite) + custom dictionary.
