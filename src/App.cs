using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Windows.Threading;

namespace Yappity
{
    // Lives in the system tray and runs the whole flow:
    // hold key -> record -> Whisper -> optional cleanup -> paste -> history.
    internal sealed class App
    {
        public const string Version = "0.2.2";
        const double MinSeconds = 0.35;
        const int MaxSeconds = 30 * 60;
        const int ChunkSeconds = 10 * 60; // Groq takes up to 25 MB per file, about 13 minutes of WAV
        const int SilencePeak = 250;

        public readonly Settings Settings;
        public readonly List<HistoryEntry> History;

        readonly NotifyIcon tray = new NotifyIcon();
        readonly KeyboardHook hook = new KeyboardHook();
        readonly Recorder recorder = new Recorder();
        readonly Bubble overlay = new Bubble();
        readonly DispatcherTimer maxTimer = new DispatcherTimer();
        readonly DispatcherTimer trimTimer = new DispatcherTimer();
        readonly Icon idleIcon, recordingIcon, workingIcon;
        MainWindow window;
        DateTime startedAt;
        int busy;      // transcriptions in flight
        int session;   // bumps on each key press so a stale release is ignored

        public App()
        {
            Settings = Store.LoadSettings();
            History = Store.LoadHistory();

            int iconSize = Math.Max(16, SystemInformation.SmallIconSize.Width);
            idleIcon = Icons.Tray(iconSize * 2, Color.Empty);
            recordingIcon = Icons.Tray(iconSize * 2, Color.FromArgb(255, 69, 58));
            workingIcon = Icons.Tray(iconSize * 2, Color.FromArgb(251, 191, 36));

            Theme.Refresh(Settings.Theme);
            BuildTrayMenu();
            tray.Icon = idleIcon;
            tray.Visible = true;
            tray.MouseClick += delegate(object s, MouseEventArgs e) { if (e.Button == MouseButtons.Left) ShowWindow(false); };
            UpdateTrayText();

            recorder.LevelChanged = overlay.PushLevel;
            maxTimer.Interval = TimeSpan.FromSeconds(MaxSeconds);
            maxTimer.Tick += delegate { maxTimer.Stop(); Finish(); };

            hook.Set(Settings.ShortcutVk, Settings.ShortcutMods, Settings.HoldMode);
            overlay.Position = Settings.Bubble ?? "top";
            hook.Pressed += Begin;
            hook.Released += Finish;
            hook.Toggled += delegate { if (recorder.IsRecording) Finish(); else Begin(); };
            hook.Interrupted += Discard;
            hook.Cancelled += delegate
            {
                if (!recorder.IsRecording) return;
                Discard();
                overlay.ShowMessage("Cancelled", Bubble.Grey, 900);
            };
            hook.Install();

            PruneAudio();
            trimTimer.Interval = TimeSpan.FromSeconds(4);
            trimTimer.Tick += delegate
            {
                // Hand unused memory back to Windows a few seconds after each yap, then
                // every two minutes while Yappity just sits in the tray.
                trimTimer.Stop();
                if (!recorder.IsRecording && busy == 0 && window == null) Native.TrimMemory();
                trimTimer.Interval = TimeSpan.FromMinutes(2);
                trimTimer.Start();
            };

            if (string.IsNullOrEmpty(Settings.ApiKey))
            {
                ShowWindow(true);
            }
            else
            {
                tray.ShowBalloonTip(3000, "Yappity is on", HowTo() + " It types wherever your cursor is. Search for Yappity in the Start menu to open it again.", ToolTipIcon.None);
                ScheduleTrim();
            }
        }

        void ScheduleTrim()
        {
            trimTimer.Stop();
            trimTimer.Interval = TimeSpan.FromSeconds(4);
            trimTimer.Start();
        }

        void BuildTrayMenu()
        {
            var old = tray.ContextMenuStrip;
            var menu = Theme.Menu();
            menu.Items.Add(Theme.Item("History", delegate { ShowWindow(false); }));
            menu.Items.Add(Theme.Item("Settings", delegate { ShowWindow(true); }));
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add(Theme.Item("Quit Yappity", delegate { Quit(); }));
            tray.ContextMenuStrip = menu;
            if (old != null) old.Dispose();
        }

        public void ApplySettings()
        {
            Store.SaveSettings(Settings);
            hook.Set(Settings.ShortcutVk, Settings.ShortcutMods, Settings.HoldMode);
            overlay.Position = Settings.Bubble ?? "top";
            UpdateTrayText();
        }

        // Light, dark or follow Windows. The window is rebuilt so every color updates.
        public void ApplyTheme() { ApplyTheme(true); }

        public void ApplyTheme(bool settingsTab)
        {
            Theme.Refresh(Settings.Theme);
            BuildTrayMenu();
            if (window != null)
            {
                var old = window;
                window = null;
                ShowWindow(settingsTab);
                window.WindowStartupLocation = System.Windows.WindowStartupLocation.Manual;
                window.Left = old.Left;
                window.Top = old.Top;
                window.Width = old.Width;
                window.Height = old.Height;
                old.Close();
            }
        }

        // Settings > Install: copy to %LOCALAPPDATA%\Programs\Yappity and restart from there.
        public void InstallAndRestart()
        {
            Installer.Install();
            Installer.RelaunchInstalled();
            Quit();
        }

        public string ShortcutName { get { return KeyboardHook.Describe(Settings.ShortcutVk, Settings.ShortcutMods); } }

        // One sentence on how to start a yap with the current settings.
        public string HowTo()
        {
            return Settings.HoldMode
                ? "Hold " + ShortcutName + " while you talk, then let go."
                : (KeyboardHook.IsModifier(Settings.ShortcutVk) && Settings.ShortcutMods == 0 ? "Tap " : "Press ") + ShortcutName + " to start talking and again when you're done.";
        }

        void UpdateTrayText()
        {
            string t = "Yappity Â· " + (Settings.HoldMode ? "hold " : "press ") + ShortcutName;
            tray.Text = t.Length > 63 ? t.Substring(0, 63) : t;
        }

        // While the user records a new shortcut in Settings.
        public void CaptureShortcut(Action<int, int> done) { hook.Capture(done); }
        public void CancelCapture() { hook.CancelCapture(); }

        void SetTray(Icon icon) { tray.Icon = icon; }

        // ---- Recording ----

        void Begin()
        {
            if (recorder.IsRecording) return;
            session++;
            if (string.IsNullOrEmpty(Settings.ApiKey))
            {
                overlay.ShowMessage("Add your Groq key in Settings first. It's free.", Bubble.Amber, 2500);
                ShowWindow(true);
                return;
            }
            try
            {
                recorder.Start();
            }
            catch (Exception e)
            {
                Store.Log(e.Message);
                overlay.ShowMessage(e.Message, Bubble.Amber, 5000);
                return;
            }
            startedAt = DateTime.UtcNow;
            hook.Recording = true;
            maxTimer.Start();
            SetTray(recordingIcon);
            overlay.ShowListening();
        }

        // Throws the recording away (Esc, or the key was part of a normal shortcut).
        void Discard()
        {
            if (!recorder.IsRecording) return;
            session++;
            maxTimer.Stop();
            hook.Recording = false;
            recorder.Stop();
            overlay.HideNow();
            SetTray(busy > 0 ? workingIcon : idleIcon);
        }

        async void Finish()
        {
            if (!recorder.IsRecording) return;
            int mine = session;
            // Keep recording a moment so the last word is not cut off.
            await Task.Delay(180);
            if (mine != session || !recorder.IsRecording) return;

            maxTimer.Stop();
            hook.Recording = false;
            byte[] pcm = recorder.Stop();
            double seconds = pcm.Length / (double)Recorder.BytesPerSecond;

            if (seconds < MinSeconds + 0.18)
            {
                overlay.HideNow();
                SetTray(busy > 0 ? workingIcon : idleIcon);
                return;
            }
            if (Recorder.Peak(pcm) < SilencePeak)
            {
                overlay.ShowMessage("Didn't hear anything. Is your mic on?", Bubble.Grey, 2200);
                SetTray(busy > 0 ? workingIcon : idleIcon);
                return;
            }

            await Process(Recorder.ToWav(pcm), seconds, null, true);
        }

        // Runs Whisper and cleanup. `existing` is set when retrying an entry from History.
        // The recording is saved to disk first, so it survives even if Groq fails.
        async Task Process(byte[] wav, double seconds, HistoryEntry existing, bool pasteIntoActiveWindow)
        {
            busy++;
            SetTray(workingIcon);
            overlay.ShowWorking("Writing it down");
            string key = Settings.ApiKey;
            HistoryEntry entry = existing;
            string id = existing != null ? existing.Id : Guid.NewGuid().ToString("N");
            string audio = existing != null && !string.IsNullOrEmpty(existing.AudioFile) && File.Exists(existing.AudioFile)
                ? existing.AudioFile : null;
            try { if (audio == null) audio = Store.SaveAudio(id, wav); }
            catch (Exception e) { Store.Log("Couldn't save the recording: " + e.Message); }
            try
            {
                string raw = await TranscribeAll(key, wav);
                if (IsNothing(raw, seconds))
                {
                    if (existing == null) DeleteFile(audio);
                    overlay.ShowMessage("Didn't catch any words there.", Bubble.Grey, 2000);
                    return;
                }
                raw = Tidy(raw);

                string text = raw;
                string cleanupModel = Groq.Find(Groq.CleanupModels, Settings.CleanupModel).Id;
                if (cleanupModel.Length > 0 && Settings.Style != "raw")
                {
                    overlay.ShowWorking("Tidying up");
                    text = await CleanupWithBackup(key, cleanupModel, raw);
                }
                text = Tidy(text);

                if (entry == null)
                {
                    entry = new HistoryEntry();
                    entry.Id = id;
                    entry.TimeUtc = DateTime.UtcNow;
                    entry.Seconds = seconds;
                    History.Insert(0, entry);
                }
                entry.Raw = raw;
                entry.Text = text;
                entry.Failed = false;
                entry.Error = null;
                entry.AudioFile = audio;
                if (Settings.KeepAudioDays <= 0) Store.DeleteAudio(entry);
                Store.SaveHistory(History);
                RefreshWindow();

                if (!pasteIntoActiveWindow)
                {
                    overlay.ShowMessage("Done. It's in History now.", Bubble.Green, 1600);
                    return;
                }
                OutputResult result = await Paster.Deliver(text, Settings.Output);
                if (result == OutputResult.Typed || result == OutputResult.Pasted)
                    overlay.ShowMessage("Done", Bubble.Green, 1100);
                else if (result == OutputResult.SavedOnly)
                    overlay.ShowMessage("Saved to History", Bubble.Green, 1400);
                else if (result == OutputResult.AdminWindow)
                    overlay.ShowMessage("Windows won't let apps type into admin windows. Copy it from History.", Bubble.Amber, 4000);
                else
                    overlay.ShowMessage("Couldn't type it here. Copy it from History.", Bubble.Amber, 3500);
            }
            catch (GroqException e)
            {
                Store.Log("Transcription failed: " + e.Message);
                if (entry == null)
                {
                    entry = new HistoryEntry();
                    entry.Id = id;
                    entry.TimeUtc = DateTime.UtcNow;
                    entry.Seconds = seconds;
                    History.Insert(0, entry);
                }
                entry.Failed = true;
                entry.Error = e.Message;
                entry.AudioFile = audio; // failed recordings are always kept so you can retry them
                Store.SaveHistory(History);
                RefreshWindow();
                overlay.ShowMessage(e.Message + " The recording is saved in History.", Bubble.Amber, 5000);
            }
            catch (Exception e)
            {
                Store.Log("Unexpected error: " + e);
                overlay.ShowMessage("That broke: " + e.Message, Bubble.Amber, 5000);
            }
            finally
            {
                busy--;
                if (!recorder.IsRecording) SetTray(busy > 0 ? workingIcon : idleIcon);
                ScheduleTrim();
            }
        }

        // Tries the chosen cleanup model, then one other model (each has its own
        // free limit), and finally gives back the raw text so a yap never gets lost.
        async Task<string> CleanupWithBackup(string key, string model, string raw)
        {
            // Qwen falls back to gpt-oss-120b, and the gpt-oss models fall back to Qwen.
            string backup = model == Groq.CleanupModels[0].Id ? Groq.CleanupModels[1].Id : Groq.CleanupModels[0].Id;
            foreach (string m in new[] { model, backup })
            {
                try { return await Groq.Cleanup(key, m, raw, Settings.Style); }
                catch (GroqException e) { Store.Log("Cleanup with " + m + " failed: " + e.Message); }
            }
            return raw;
        }

        // Long recordings go to Groq in parts of up to 10 minutes, each cut at the
        // quietest moment near the boundary so no word gets split in half.
        Task<string> TranscribeAll(string key, byte[] wav)
        {
            return TranscribeLong(key, Groq.Find(Groq.SpeechModels, Settings.SpeechModel).Id, wav,
                Settings.Language, Settings.Vocabulary, ChunkSeconds,
                delegate(int part, int total) { overlay.ShowWorking("Writing it down, part " + part + " of " + total); });
        }

        public static async Task<string> TranscribeLong(string key, string model, byte[] wav, string language,
            string vocabulary, int chunkSeconds, Action<int, int> progress)
        {
            byte[] pcm = new byte[Math.Max(0, wav.Length - 44)];
            Buffer.BlockCopy(wav, Math.Min(44, wav.Length), pcm, 0, pcm.Length);
            int chunkBytes = chunkSeconds * Recorder.BytesPerSecond;
            if (pcm.Length <= chunkBytes)
                return await Groq.Transcribe(key, model, wav, language, vocabulary);

            var parts = new List<string>();
            int start = 0, index = 0, total = (pcm.Length + chunkBytes - 1) / chunkBytes;
            while (start < pcm.Length)
            {
                int end = pcm.Length;
                if (pcm.Length - start > chunkBytes)
                {
                    // Look for a pause in the last 5 seconds (or last half) of this part.
                    int window = Math.Min(5 * Recorder.BytesPerSecond, chunkBytes / 2);
                    end = QuietestCut(pcm, start + chunkBytes - window, start + chunkBytes);
                    end -= end % 2;
                    if (end <= start) end = start + chunkBytes;
                }
                var piece = new byte[end - start];
                Buffer.BlockCopy(pcm, start, piece, 0, piece.Length);
                index++;
                if (progress != null) progress(index, Math.Max(total, index));
                string text = await Groq.Transcribe(key, model, Recorder.ToWav(piece), language, vocabulary);
                if (!string.IsNullOrWhiteSpace(text)) parts.Add(text.Trim());
                start = end;
            }
            return string.Join(" ", parts);
        }

        // Finds the quietest 100 ms between two byte offsets and returns where it starts.
        static int QuietestCut(byte[] pcm, int from, int to)
        {
            int frame = Recorder.BytesPerSecond / 10;
            int best = to;
            double bestEnergy = double.MaxValue;
            for (int f = Math.Max(0, from); f + frame <= Math.Min(to, pcm.Length); f += frame)
            {
                double e = 0;
                for (int i = f; i + 1 < f + frame; i += 2)
                {
                    int s = (short)(pcm[i] | (pcm[i + 1] << 8));
                    e += (double)s * s;
                }
                if (e < bestEnergy) { bestEnergy = e; best = f; }
            }
            return best;
        }

        static void DeleteFile(string path)
        {
            try { if (path != null && File.Exists(path)) File.Delete(path); } catch (Exception) { }
        }

        // No em dashes or en dashes in the final text, ever. Commas read more human.
        public static string Tidy(string text)
        {
            if (string.IsNullOrEmpty(text)) return "";
            string t = text.Replace(" \u2014 ", ", ").Replace(" \u2013 ", ", ").Replace("\u2014", ", ").Replace("\u2013", "-");
            while (t.Contains(" ,")) t = t.Replace(" ,", ",");
            while (t.Contains(",,")) t = t.Replace(",,", ",");
            while (t.Contains("  ")) t = t.Replace("  ", " ");
            t = t.Replace("\r\n", "\n");
            while (t.Contains("\n\n\n")) t = t.Replace("\n\n\n", "\n\n");
            return t.Trim();
        }

        // Deletes recordings older than the "Keep recordings" setting. Recordings that
        // failed to send are kept for 30 days so they can still be retried.
        public void PruneAudio()
        {
            bool changed = false;
            var now = DateTime.UtcNow;
            foreach (var e in History)
            {
                if (string.IsNullOrEmpty(e.AudioFile)) continue;
                int days = e.Failed ? Math.Max(30, Settings.KeepAudioDays) : Settings.KeepAudioDays;
                if (!File.Exists(e.AudioFile) || (now - e.TimeUtc).TotalDays > days) { Store.DeleteAudio(e); changed = true; }
            }
            // Recordings no history item points to (for example after "Clear all").
            try
            {
                var known = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (var e in History) if (!string.IsNullOrEmpty(e.AudioFile)) known.Add(e.AudioFile);
                if (Directory.Exists(Store.AudioDir))
                    foreach (string f in Directory.GetFiles(Store.AudioDir, "*.wav"))
                        if (!known.Contains(f) && (now - File.GetLastWriteTimeUtc(f)).TotalHours > 1) DeleteFile(f);
            }
            catch (Exception) { }
            if (changed) Store.SaveHistory(History);
        }

        // Whisper sometimes "hears" these in near-silence.
        static readonly string[] Phantoms =
        {
            "", ".", "you", "thank you", "thank you.", "thanks.", "thanks for watching!", "thanks for watching.",
            "thank you for watching.", "thank you for watching!", "bye.", "bye!", "okay.", "so"
        };

        static bool IsNothing(string raw, double seconds)
        {
            string t = (raw ?? "").Trim().ToLowerInvariant();
            if (t.Length == 0) return true;
            return seconds < 3 && Array.IndexOf(Phantoms, t) >= 0;
        }

        public async void Retry(HistoryEntry entry)
        {
            if (entry == null || string.IsNullOrEmpty(entry.AudioFile) || !File.Exists(entry.AudioFile)) return;
            overlay.ShowWorking("Writing it down");
            byte[] wav = File.ReadAllBytes(entry.AudioFile);
            await Process(wav, entry.Seconds, entry, false);
        }

        // ---- Window ----

        public void ShowWindow(bool settings)
        {
            if (window == null)
            {
                var w = new MainWindow(this);
                w.Closed += delegate { if (window == w) window = null; ScheduleTrim(); };
                window = w;
            }
            window.ShowTab(settings);
        }

        void RefreshWindow()
        {
            if (window != null) window.ReloadHistory();
        }

        public void RefreshHistory() { RefreshWindow(); }

        public void Quit()
        {
            hook.Dispose();
            hook.Recording = false;
            if (recorder.IsRecording) recorder.Stop();
            tray.Visible = false;
            tray.Dispose();
            overlay.Close();
            if (window != null) window.Close();
            System.Windows.Application.Current.Shutdown();
        }
    }
}
