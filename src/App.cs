using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Yappity
{
    // Lives in the system tray and runs the whole flow:
    // hold key -> record -> Whisper -> optional cleanup -> paste -> history.
    internal sealed class App : ApplicationContext
    {
        public const string Version = "0.1.0";
        const double MinSeconds = 0.35;
        const int MaxSeconds = 10 * 60;
        const int SilencePeak = 250;

        public readonly Settings Settings;
        public readonly List<HistoryEntry> History;

        readonly NotifyIcon tray = new NotifyIcon();
        readonly KeyboardHook hook = new KeyboardHook();
        readonly Recorder recorder = new Recorder();
        readonly Overlay overlay = new Overlay();
        readonly Timer maxTimer = new Timer();
        readonly Timer trimTimer = new Timer();
        readonly Icon idleIcon, recordingIcon, workingIcon;
        MainForm window;
        DateTime startedAt;
        int busy;      // transcriptions in flight
        int session;   // bumps on each key press so a stale release is ignored

        public App()
        {
            Settings = Store.LoadSettings();
            History = Store.LoadHistory();

            int iconSize = Math.Max(16, SystemInformation.SmallIconSize.Width);
            idleIcon = Icons.Make(iconSize * 2, Icons.IdleColor);
            recordingIcon = Icons.Make(iconSize * 2, Icons.RecordingColor);
            workingIcon = Icons.Make(iconSize * 2, Icons.WorkingColor);

            var menu = Theme.Menu();
            menu.Items.Add(Theme.Item("History", delegate { ShowWindow(false); }));
            menu.Items.Add(Theme.Item("Settings", delegate { ShowWindow(true); }));
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add(Theme.Item("Quit Yappity", delegate { ExitThread(); }));
            tray.ContextMenuStrip = menu;
            tray.Icon = idleIcon;
            tray.Visible = true;
            tray.MouseClick += delegate(object s, MouseEventArgs e) { if (e.Button == MouseButtons.Left) ShowWindow(false); };
            UpdateTrayText();

            recorder.LevelChanged = overlay.PushLevel;
            maxTimer.Interval = MaxSeconds * 1000;
            maxTimer.Tick += delegate { maxTimer.Stop(); OnReleased(); };

            hook.Key = KeyboardHook.Find(Settings.HotkeyVk);
            hook.Pressed += OnPressed;
            hook.Released += OnReleased;
            hook.Interrupted += OnInterrupted;
            hook.Install();

            trimTimer.Interval = 4000;
            trimTimer.Tick += delegate
            {
                trimTimer.Stop();
                if (!recorder.IsRecording && busy == 0 && window == null) Native.TrimMemory();
            };

            if (string.IsNullOrEmpty(Settings.ApiKey))
            {
                ShowWindow(true);
            }
            else
            {
                tray.ShowBalloonTip(3000, "Yappity is on",
                    "Hold " + hook.Key.Name + ", yap, let go. It types wherever your cursor is.", ToolTipIcon.None);
                ScheduleTrim();
            }
        }

        void ScheduleTrim()
        {
            trimTimer.Stop();
            trimTimer.Start();
        }

        public void ApplySettings()
        {
            Store.SaveSettings(Settings);
            hook.Key = KeyboardHook.Find(Settings.HotkeyVk);
            UpdateTrayText();
        }

        void UpdateTrayText()
        {
            tray.Text = "Yappity · hold " + hook.Key.Name + " to yap";
        }

        void SetTray(Icon icon) { tray.Icon = icon; }

        // ---- Recording ----

        void OnPressed()
        {
            if (recorder.IsRecording) return;
            session++;
            if (string.IsNullOrEmpty(Settings.ApiKey))
            {
                overlay.ShowMessage("Add your Groq key in Settings first. It's free.", Overlay.Amber, 2500);
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
                overlay.ShowMessage(e.Message, Overlay.Amber, 5000);
                return;
            }
            startedAt = DateTime.UtcNow;
            maxTimer.Start();
            SetTray(recordingIcon);
            overlay.ShowListening();
        }

        void OnInterrupted()
        {
            // The key was used as part of a shortcut, not for dictation.
            if (!recorder.IsRecording) return;
            maxTimer.Stop();
            recorder.Stop();
            overlay.HideNow();
            SetTray(busy > 0 ? workingIcon : idleIcon);
        }

        async void OnReleased()
        {
            if (!recorder.IsRecording) return;
            int mine = session;
            // Keep recording a moment so the last word is not cut off.
            await Task.Delay(180);
            if (mine != session || !recorder.IsRecording) return;

            maxTimer.Stop();
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
                overlay.ShowMessage("Didn't hear anything. Is your mic on?", Overlay.Muted, 2200);
                SetTray(busy > 0 ? workingIcon : idleIcon);
                return;
            }

            await Process(Recorder.ToWav(pcm), seconds, null, true);
        }

        // Runs Whisper and cleanup. `existing` is set when retrying a failed entry.
        async Task Process(byte[] wav, double seconds, HistoryEntry existing, bool pasteIntoActiveWindow)
        {
            busy++;
            SetTray(workingIcon);
            overlay.ShowWorking("Writing it down");
            string key = Settings.ApiKey;
            HistoryEntry entry = existing;
            try
            {
                string raw = await Groq.Transcribe(key, wav, Settings.Language, Settings.Vocabulary);
                if (IsNothing(raw, seconds))
                {
                    overlay.ShowMessage("Didn't catch any words there.", Overlay.Muted, 2000);
                    return;
                }

                string text = raw;
                if (Settings.Cleanup)
                {
                    overlay.ShowWorking("Tidying up");
                    try { text = await Groq.Cleanup(key, raw); }
                    catch (GroqException e) { Store.Log("Cleanup skipped: " + e.Message); text = raw; }
                }
                text = text.Trim();

                if (entry == null)
                {
                    entry = new HistoryEntry();
                    entry.Id = Guid.NewGuid().ToString("N");
                    entry.TimeUtc = DateTime.UtcNow;
                    entry.Seconds = seconds;
                    History.Insert(0, entry);
                }
                entry.Raw = raw;
                entry.Text = text;
                entry.Failed = false;
                entry.Error = null;
                Store.DeleteAudio(entry);
                Store.SaveHistory(History);
                RefreshWindow();

                PasteResult result = await Paster.Paste(text,
                    pasteIntoActiveWindow && Settings.AutoPaste, Settings.RestoreClipboard);
                if (result == PasteResult.Pasted)
                    overlay.ShowMessage("Done", Overlay.Green, 1100);
                else if (result == PasteResult.CopiedOnly)
                    overlay.ShowMessage(pasteIntoActiveWindow && Settings.AutoPaste
                        ? "Copied. This app runs as admin, so press Ctrl+V yourself."
                        : "Copied. Press Ctrl+V.", Overlay.Green, 2600);
                else
                    overlay.ShowMessage("Clipboard was busy. Your text is saved in History.", Overlay.Amber, 3500);
            }
            catch (GroqException e)
            {
                Store.Log("Transcription failed: " + e.Message);
                if (entry == null)
                {
                    entry = new HistoryEntry();
                    entry.Id = Guid.NewGuid().ToString("N");
                    entry.TimeUtc = DateTime.UtcNow;
                    entry.Seconds = seconds;
                    History.Insert(0, entry);
                }
                entry.Failed = true;
                entry.Error = e.Message;
                if (e.CanRetry && string.IsNullOrEmpty(entry.AudioFile))
                    entry.AudioFile = Store.SaveAudio(entry.Id, wav);
                Store.SaveHistory(History);
                RefreshWindow();
                overlay.ShowMessage(e.Message + (e.CanRetry ? " Retry it from History." : ""), Overlay.Amber, 5000);
            }
            catch (Exception e)
            {
                Store.Log("Unexpected error: " + e);
                overlay.ShowMessage("That broke: " + e.Message, Overlay.Amber, 5000);
            }
            finally
            {
                busy--;
                if (!recorder.IsRecording) SetTray(busy > 0 ? workingIcon : idleIcon);
                ScheduleTrim();
            }
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
            byte[] wav = File.ReadAllBytes(entry.AudioFile);
            await Process(wav, entry.Seconds, entry, false);
        }

        // ---- Window ----

        public void ShowWindow(bool settings)
        {
            if (window == null || window.IsDisposed)
            {
                window = new MainForm(this);
                window.FormClosed += delegate { window = null; ScheduleTrim(); };
            }
            window.ShowTab(settings);
        }

        void RefreshWindow()
        {
            if (window != null && !window.IsDisposed) window.ReloadHistory();
        }

        protected override void ExitThreadCore()
        {
            hook.Dispose();
            if (recorder.IsRecording) recorder.Stop();
            tray.Visible = false;
            tray.Dispose();
            overlay.Close();
            if (window != null && !window.IsDisposed) window.Close();
            base.ExitThreadCore();
        }
    }
}
