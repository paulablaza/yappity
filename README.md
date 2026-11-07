# Yappity

Hold a key, yap, let go. Whatever you said gets typed where your cursor is.

Yappity is a tiny voice typing app for Windows. It runs on Groq's free API, so you bring your own key
and pay nothing.

---

## Why this exists

I wanted something like Wispr Flow without the subscription. The open source ones I found either run
Whisper on your own PC (slow if your PC isn't strong) or ship a whole browser engine just to show a
settings window.

Yappity sends your audio to Groq instead, so it's fast even on a cheap laptop. The app itself is one
100 KB `.exe`, and it sits in your tray using less than 1 MB of memory while you're not talking.

---

## What it does

1. You hold **Right Ctrl** and talk. A small bubble near the bottom of your screen shows your voice level.
2. You let go. **Whisper Large v3** turns it into text, usually in about a second.
3. **gpt-oss-20b** tidies it up. It takes out the um, uh, you know, repeated words and false starts,
   and fixes punctuation. If you say "at 3, no wait, at 4", you get "at 4". It doesn't rewrite your
   sentences.
4. Yappity pastes it into whatever app you're in, then puts your clipboard back the way it was.

Every yap also lands in **History**. If a paste didn't go through, you can copy it from there. If your
internet dropped, the audio is saved there too so you can retry it.

---

## Setup

1. Download `Yappity.exe` from [Releases](../../releases) and run it.
   Windows will probably say it doesn't recognize the app because it isn't code signed.
   Click **More info**, then **Run anyway**.
2. Get a free key at [console.groq.com/keys](https://console.groq.com/keys) and paste it in Settings.
3. Click into any text box, hold Right Ctrl, and say something.

Nothing else to install. It runs on the .NET Framework that already comes with Windows 10 and 11.

---

## Settings

| Setting | What it does |
| :--- | :--- |
| Hold to yap | Pick your key. Right Ctrl, Right Alt, Right Shift, F8, F9, F10, Scroll Lock or Pause |
| Language | Leave it empty and Whisper figures it out. Or type a code like `en`, `tl` or `es` |
| Clean up with AI | Turn it off if you want exactly what Whisper heard |
| Paste automatically | Turn it off if you'd rather paste it yourself |
| Keep my clipboard | Puts back whatever you copied before Yappity pasted |
| Start with Windows | Opens Yappity when you sign in |
| Words to spell right | Names and slang Whisper keeps getting wrong, separated by commas |

If you press another key while holding Right Ctrl (like Right Ctrl + C), Yappity cancels the recording,
so your normal shortcuts still work.

Windows blocks normal apps from typing into apps running as admin. In those, Yappity copies the text
and tells you to press Ctrl+V yourself.

---

## Free tier limits

Groq's free plan gives `whisper-large-v3` 20 requests a minute and 2,000 a day, with up to 2 hours of
audio an hour. Each request counts as at least 10 seconds, so that works out to around 2,000 yaps a day.
That's plenty for one person.

*(Groq sets these and can change them. Check [console.groq.com/docs/rate-limits](https://console.groq.com/docs/rate-limits).)*

If you hit the limit, Yappity tells you and keeps the recording in History so you can retry later.

---

## Privacy

Your audio goes to Groq and nowhere else. Your key is encrypted on your PC with Windows DPAPI, so
only your Windows account can read it. History stays on your PC in `%APPDATA%\Yappity`.

---

## Build it yourself

You don't need Visual Studio or any SDK. Windows already has a C# compiler.

```powershell
powershell -ExecutionPolicy Bypass -File build.ps1
```

That gives you `bin\Yappity.exe`.

| File | What's in it |
| :--- | :--- |
| `src/App.cs` | Tray icon and the whole hold, record, transcribe, paste flow |
| `src/KeyboardHook.cs` | Watches for your hold to talk key in any app |
| `src/Recorder.cs` | Mic recording at 16 kHz mono using the Windows waveIn API |
| `src/Groq.cs` | Whisper and cleanup requests |
| `src/Paster.cs` | Clipboard and Ctrl+V |
| `src/Overlay.cs` | The little bubble while you talk |
| `src/MainForm.cs`, `src/HistoryView.cs`, `src/Ui.cs` | History and Settings window |
| `src/Store.cs` | Settings, history and logs |

Two commands for testing without the mic or hotkey:

```powershell
# Send a WAV file through Whisper and cleanup (uses GROQ_API_KEY or your saved key)
bin\Yappity.exe --test sample.wav result.txt

# Record a few seconds from your mic
bin\Yappity.exe --record-test 3 mic.wav
```

Set `YAPPITY_HOME` if you want settings and history in a different folder.

---

## License

MIT
