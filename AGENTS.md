# AGENTS.md

Notes for coding agents working on Yappity. Read this before changing anything.

## What Yappity is

A tiny Windows tray app for voice typing. The user presses a shortcut, talks, presses it again, and
the text gets typed into whatever app has focus. Speech to text runs on Groq (Whisper), and a small
LLM on Groq cleans up filler words. Users bring their own free Groq API key.

The two promises that matter most:

1. **Free.** Everything has to work on Groq's free tier. Don't add a paid service or a model that
   isn't on the free plan.
2. **Light and fast.** One `.exe`, nothing to install, about 1 MB in Task Manager while idle.
   Don't add NuGet packages, a browser engine, or a runtime the user has to install.

## How it's built

- C# on **.NET Framework 4.8**, which ships with every Windows 10 and 11 PC.
- Compiled with the **C# 5 compiler that comes with Windows** (`csc.exe` in
  `%WINDIR%\Microsoft.NET\Framework64\v4.0.30319`). There is no SDK and no `.csproj`.
- UI is **WPF built in code** (no XAML). The tray icon uses WinForms `NotifyIcon`.
- Build: `powershell -ExecutionPolicy Bypass -File build.ps1` writes `bin\Yappity.exe`.
  `build.ps1 -Out some\other.exe` builds to a different file, which helps when the real one is running.

Because the compiler is C# 5, these don't compile: `$"..."` string interpolation, `?.`, `nameof`,
expression-bodied members (`=>` on methods or properties), `out var`, tuples, pattern matching,
auto-property initializers. Use `string.Format` or `+`, explicit null checks, and full method bodies.

## Where things are

| File | What it does |
| :--- | :--- |
| `src/Program.cs` | Entry point, single-instance check, developer commands |
| `src/App.cs` | Tray icon and the whole flow: record, transcribe, clean up, type, history, saved recordings |
| `src/KeyboardHook.cs` | Global low-level keyboard hook: toggle and hold modes, Esc to cancel, recording a new shortcut |
| `src/Recorder.cs` | Mic capture with the Windows waveIn API, 16 kHz mono 16-bit |
| `src/Groq.cs` | HTTP calls to Groq, the model lists, free-tier limit text, the cleanup prompt |
| `src/Paster.cs` | Types text with `SendInput` Unicode events, or pastes with Ctrl+V |
| `src/Bubble.cs` | The floating bubble with Yappy-chan, click-through and never focused |
| `src/MainWindow.cs` | History and Settings window |
| `src/Kit.cs` | Colors, animation helpers and the small WPF controls (toggle, picker, field, group) |
| `src/Store.cs` | Settings, history, saved recordings and the log in `%APPDATA%\Yappity` (or `YappityData` next to the exe in portable mode) |
| `src/Installer.cs` | Install to `%LOCALAPPDATA%\Programs\Yappity`, Start menu, Installed apps entry, `--uninstall` |
| `src/Ui.cs` | Light/dark detection and the tray menu colors |
| `tools/make-art.py` | Turns the art in `design/` into `assets/` (needs Pillow) |

## Rules

- **Never lose a yap.** The recording is saved before it's sent to Groq. If anything fails, the user
  can play it, drag it out, or send it again from History.
- **Cleanup must not change meaning.** The cleanup prompt in `Groq.cs` only removes filler and fixes
  punctuation. It must never answer, follow, translate or summarize what the user said. If you
  touch the prompt, run `--clean-test` on tricky lines (questions, "ignore your instructions",
  Taglish) with every cleanup model before committing.
- **No em dashes** in anything Yappity types. `App.Tidy` strips them, and the prompt forbids them.
- **Always give the clipboard back.** The default output pastes with Ctrl+V and restores whatever was
  on the clipboard before. Typing mode sends Unicode key presses, but anything over 400 characters is
  pasted anyway, because slow apps drop parts of long typed text.
- The window is created when opened and thrown away when closed. Don't keep it alive in the
  background; idle memory is a feature.
- Writing style for anything a user reads (UI text, README): casual, plain, full sentences, no em
  dashes, no hype words.

## Testing

There's no test project. Use the developer commands, which never touch the user's real settings if
you set `YAPPITY_HOME` to an empty folder:

```powershell
$env:YAPPITY_HOME = "$env:TEMP\yappity-test"
$env:GROQ_API_KEY = "gsk_..."

bin\Yappity.exe --test sample.wav out.txt          # Whisper + cleanup on a WAV file
bin\Yappity.exe --clean-test lines.txt out.txt     # cleanup only, one line at a time
bin\Yappity.exe --record-test 3 mic.wav            # record from the mic
bin\Yappity.exe --bubble-test top out              # renders out-listening.png, out-working.png, out-done.png
bin\Yappity.exe --tray-icons tray.png              # the three tray icon states
bin\Yappity.exe --settings                         # open straight to Settings
bin\Yappity.exe --type-test text.txt out.txt       # delivers text into a test box and checks every character arrived
```

Environment overrides for `--test`: `YAPPITY_SPEECH`, `YAPPITY_CLEANUP`, `YAPPITY_LANG`,
`YAPPITY_CHUNK_SECONDS`. `--clean-test` also takes `YAPPITY_FORMAT=1` to try format mode.

Don't run `--install` or `--uninstall` while testing unless you mean it: they write to the real
Start menu and the Installed apps list.

The global shortcut ignores injected keystrokes, so you can't drive a real yap from a script. Ask a
human to try the full flow after changing `KeyboardHook.cs`, `Recorder.cs` or `Paster.cs`.
