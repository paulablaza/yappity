# Saywrite

Hold a key, talk, let go. Your words get typed wherever your cursor is.

Saywrite is a tiny voice-dictation app for Windows. It uses [Groq](https://groq.com)'s free API with
**Whisper Large v3** for speech to text and **gpt-oss-20b** to tidy the result: it removes "um", "uh",
"you know", repeated words and false starts, and fixes punctuation, without rewriting what you said.

- **Tiny.** About 100 KB, one `.exe`, nothing to install. It runs on the .NET Framework that already
  ships with every Windows 10 and 11 PC.
- **Light.** It sits in the tray using under 1 MB of memory in Task Manager while idle.
- **Free.** Bring your own Groq API key. Groq's free plan covers about 2,000 dictations a day.
- **Nothing gets lost.** Every dictation is kept in History, so if a paste didn't land you can copy it
  again. Failed uploads keep their audio so you can retry.
- **Private.** Your audio goes only to Groq. Your key is encrypted on your PC with Windows DPAPI.

## Get started

1. Download `Saywrite.exe` from [Releases](../../releases) and run it. Windows may warn that the app
   is unrecognized because it isn't code-signed: click **More info**, then **Run anyway**.
2. Get a free API key at [console.groq.com/keys](https://console.groq.com/keys) and paste it into
   Settings.
3. Click into any text box, **hold Right Ctrl**, speak, and let go.

While you hold the key a small "Listening" bubble shows your voice level. When you let go the text is
transcribed, cleaned up and pasted, usually within a second or two.

## Settings

| Setting | What it does |
|---|---|
| Hold to talk | The dictation key: Right Ctrl (default), Right Alt, Right Shift, F8, F9, F10, Scroll Lock or Pause. |
| Language | Leave it empty to auto-detect, or set a code like `en`, `tl` or `es`. |
| Clean up with AI | Removes filler words and fixes punctuation. Turn it off for Whisper's raw output. |
| Paste automatically | Types into the active window. When off, the text is only copied. |
| Keep my clipboard | Restores whatever you had copied before Saywrite pasted. |
| Start with Windows | Launches Saywrite when you sign in. |
| Words to spell right | Names and jargon Whisper should spell correctly, separated by commas. |

Pressing another key while holding the dictation key (like Right Ctrl+C) cancels the recording, so
your normal shortcuts keep working.

Windows does not let normal apps type into programs running as administrator. In those windows
Saywrite copies the text and tells you to press Ctrl+V.

## Groq free plan limits

At the time of writing, for `whisper-large-v3`: 20 requests a minute, 2,000 a day, and 2 hours of
audio per hour (8 per day). Each request counts as at least 10 seconds of audio. Check
[console.groq.com/docs/rate-limits](https://console.groq.com/docs/rate-limits) for current numbers.
If you hit a limit, Saywrite says so and keeps the recording in History to retry.

## Build from source

No SDK needed. Windows already includes the C# compiler:

```powershell
powershell -ExecutionPolicy Bypass -File build.ps1
```

This writes `bin\Saywrite.exe`. The code is in `src\`:

| File | Purpose |
|---|---|
| `App.cs` | Tray icon and the dictation flow |
| `KeyboardHook.cs` | System-wide hold-to-talk key |
| `Recorder.cs` | Microphone capture (16 kHz mono, via the Windows waveIn API) |
| `Groq.cs` | Whisper and cleanup requests |
| `Paster.cs` | Clipboard and Ctrl+V |
| `Overlay.cs` | The "Listening" bubble |
| `MainForm.cs`, `HistoryView.cs`, `Ui.cs` | History and Settings window |
| `Store.cs` | Settings, history and logs in `%APPDATA%\Saywrite` |

Developer commands:

```powershell
# Send a WAV file through the same Whisper + cleanup path (uses GROQ_API_KEY or your saved key)
bin\Saywrite.exe --test sample.wav result.txt

# Record a few seconds from the microphone
bin\Saywrite.exe --record-test 3 mic.wav
```

Set `SAYWRITE_HOME` to keep settings and history in another folder.

## License

MIT
