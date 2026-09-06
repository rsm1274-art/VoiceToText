# Murmur

Local push-to-talk dictation for Windows 10/11 (x64). No cloud calls.

## Status

Phase 1 only: app skeleton + global hotkey trigger. Built and written on a
non-Windows container — **not yet compiled or run on Windows**. Open
`Murmur.sln` in Visual Studio (or `dotnet build` / `dotnet run` from
`src/Murmur`) on a Windows machine to verify before relying on it.

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
- `src/Murmur/MainWindow.xaml(.cs)` — minimal window showing hotkey status
  and live talk state, for manual verification.

## Known limitations

- Push-to-talk requires the app to run un-elevated; some corporate AV/EDR
  tools may still block `RegisterHotKey` or the polling loop outright.
- No audio capture, STT, injection, history, or dictionary yet — those are
  later phases per the execution order in the project brief.

## Next steps (not yet done)

1. Verify this skeleton builds and the hotkey fires on real Windows.
2. Windows native speech integration (`System.Speech` / OneCore Speech API).
3. Clipboard+Ctrl-V text injection, tested against VS Code/Cursor/Slack.
4. Optional Vosk secondary engine.
5. History window (SQLite) + custom dictionary.
