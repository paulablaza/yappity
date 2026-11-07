using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace Yappity
{
    // History and Settings. Built only when opened and destroyed on close, so it
    // uses no memory while Yappity sits in the tray.
    internal sealed class MainForm : Form
    {
        readonly App app;
        readonly Panel header = new Panel();
        readonly Panel footer = new Panel();
        readonly NavTab historyTab = new NavTab("History");
        readonly NavTab settingsTab = new NavTab("Settings");
        readonly HistoryView history = new HistoryView();
        readonly SettingsView settings;
        readonly Label footerText = new Label();
        readonly LinkLabel clearLink = new LinkLabel();
        readonly Bitmap logo;
        readonly Font titleFont = Theme.Bold(12.5f);

        public MainForm(App app)
        {
            this.app = app;
            Text = "Yappity";
            AutoScaleMode = AutoScaleMode.None;
            BackColor = Theme.Bg;
            ForeColor = Theme.Text;
            Font = Theme.F(9.5f);
            ClientSize = new Size(Theme.S(560), Theme.S(640));
            MinimumSize = new Size(Theme.S(440), Theme.S(480));
            StartPosition = FormStartPosition.CenterScreen;
            try { Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath); } catch (Exception) { }
            logo = Icons.Draw(Theme.S(28), Icons.IdleColor);

            // Header: logo, name, tabs
            header.Dock = DockStyle.Top;
            header.Height = Theme.S(68);
            header.BackColor = Theme.Bg;
            header.Paint += PaintHeader;
            header.Resize += delegate { PlaceTabs(); };
            historyTab.Click += delegate { ShowTab(false); };
            settingsTab.Click += delegate { ShowTab(true); };
            header.Controls.Add(historyTab);
            header.Controls.Add(settingsTab);

            // Footer: hint and "Clear all"
            footer.Dock = DockStyle.Bottom;
            footer.Height = Theme.S(44);
            footer.BackColor = Theme.Bg;
            footer.Padding = new Padding(Theme.S(22), 0, Theme.S(22), 0);
            footer.Paint += delegate(object s, PaintEventArgs e)
            {
                using (var p = new Pen(Theme.Border)) e.Graphics.DrawLine(p, 0, 0, footer.Width, 0);
            };
            footerText.Dock = DockStyle.Fill;
            footerText.TextAlign = ContentAlignment.MiddleLeft;
            footerText.ForeColor = Theme.Muted;
            footerText.Font = Theme.F(9f);
            clearLink.Text = "Clear all";
            clearLink.Dock = DockStyle.Right;
            clearLink.AutoSize = true;
            clearLink.TextAlign = ContentAlignment.MiddleRight;
            clearLink.Padding = new Padding(0, Theme.S(13), 0, 0);
            clearLink.Font = Theme.F(9f);
            clearLink.LinkColor = Theme.Muted;
            clearLink.ActiveLinkColor = Theme.Danger;
            clearLink.LinkBehavior = LinkBehavior.HoverUnderline;
            clearLink.LinkClicked += delegate { ClearAll(); };
            footer.Controls.Add(footerText);
            footer.Controls.Add(clearLink);

            history.Dock = DockStyle.Fill;
            history.ActionClicked += OnHistoryAction;

            settings = new SettingsView(app);
            settings.Dock = DockStyle.Fill;
            settings.Visible = false;

            // Fill controls first, then edges, so docking lays them out correctly.
            Controls.Add(history);
            Controls.Add(settings);
            Controls.Add(footer);
            Controls.Add(header);

            ReloadHistory();
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            Theme.StyleTitleBar(this);
        }

        void PaintHeader(object sender, PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            int x = Theme.S(22);
            g.DrawImage(logo, x, (header.Height - logo.Height) / 2);
            TextRenderer.DrawText(g, "Yappity", titleFont,
                new Rectangle(x + logo.Width + Theme.S(10), 0, Theme.S(200), header.Height), Theme.Text,
                TextFormatFlags.VerticalCenter | TextFormatFlags.Left | TextFormatFlags.NoPrefix);
        }

        void PlaceTabs()
        {
            int right = header.Width - Theme.S(14);
            int y = (header.Height - settingsTab.Height) / 2 + Theme.S(3);
            settingsTab.Location = new Point(right - settingsTab.Width, y);
            historyTab.Location = new Point(settingsTab.Left - historyTab.Width, y);
        }

        public void ShowTab(bool showSettings)
        {
            historyTab.Active = !showSettings;
            settingsTab.Active = showSettings;
            history.Visible = !showSettings;
            settings.Visible = showSettings;
            footer.Visible = !showSettings;
            if (!Visible) Show();
            if (WindowState == FormWindowState.Minimized) WindowState = FormWindowState.Normal;
            Activate();
            if (showSettings) settings.FocusFirst();
            else history.Focus();
        }

        public void ReloadHistory()
        {
            history.HowTo = app.HowTo();
            history.SetItems(app.History);
            int n = app.History.Count;
            footerText.Text = n == 0
                ? (app.Settings.HoldMode ? "Hold " : "Press ") + app.ShortcutName + " to yap"
                : n + (n == 1 ? " yap" : " yaps") + "  ·  double-click one to copy it";
            clearLink.Visible = n > 0;
        }

        void OnHistoryAction(HistoryEntry e, string action)
        {
            if (action == "Copy" || action == "Copy original")
            {
                if (Paster.SetText(action == "Copy" ? e.Text : e.Raw)) history.Toast("Copied");
            }
            else if (action == "Retry")
            {
                history.Toast("Sending it again");
                app.Retry(e);
            }
            else if (action == "Delete")
            {
                Store.DeleteAudio(e);
                app.History.Remove(e);
                Store.SaveHistory(app.History);
                ReloadHistory();
            }
        }

        void ClearAll()
        {
            if (app.History.Count == 0) return;
            if (MessageBox.Show(this, "Delete all " + app.History.Count + " yaps? You can't undo this.", "Yappity",
                    MessageBoxButtons.OKCancel, MessageBoxIcon.Question) != DialogResult.OK) return;
            foreach (var e in app.History) Store.DeleteAudio(e);
            app.History.Clear();
            Store.SaveHistory(app.History);
            ReloadHistory();
        }
    }

    // Settings save automatically as you change them.
    internal sealed class SettingsView : Panel
    {
        static readonly Choice[] Modes =
        {
            new Choice("toggle", "Toggle",
                "Press the shortcut once to start talking and press it again when you're done. If you change your mind halfway, press Esc and the recording gets thrown away."),
            new Choice("hold", "Hold",
                "Hold the shortcut down while you talk and let go when you're done. Try not to use Shift on its own here, because Windows turns on Filter Keys when you hold Shift for 8 seconds."),
        };

        static readonly Choice[] Outputs =
        {
            new Choice("type", "Type it in",
                "Yappity types the text into the app you're using, the same way your keyboard would. Your clipboard is never touched, and every yap still gets saved in History."),
            new Choice("paste", "Paste it",
                "Yappity pastes the text with Ctrl+V and then puts back whatever you had copied before. It's a bit faster for really long yaps, but a few apps don't handle it well."),
            new Choice("history", "Save to History only",
                "Nothing gets typed anywhere. Open History and double-click a yap whenever you want to copy it."),
        };

        sealed class Row
        {
            public Label Title;
            public Control Right;
            public Label Hint;
            public Control Full;     // takes the whole width instead of title + right
            public int Gap;          // extra space before this row (section headings)
        }

        readonly App app;
        readonly List<Row> rows = new List<Row>();

        readonly InputBox keyInput = new InputBox(false);
        readonly FlatButton showBtn = new FlatButton("Show", false);
        readonly FlatButton testBtn = new FlatButton("Test", false);
        readonly LinkLabel keyLink = new LinkLabel();
        readonly Label keyStatus = MakeHint("");

        readonly FlatButton shortcutBtn = new FlatButton("", false);
        readonly Label shortcutHint = MakeHint("");
        readonly ChoiceButton mode = new ChoiceButton();
        readonly Label modeHint = MakeHint("");
        readonly ChoiceButton output = new ChoiceButton();
        readonly Label outputHint = MakeHint("");

        readonly ChoiceButton speech = new ChoiceButton();
        readonly Label speechHint = MakeHint("");
        readonly ChoiceButton cleanup = new ChoiceButton();
        readonly Label cleanupHint = MakeHint("");
        readonly ChoiceButton language = new ChoiceButton();
        readonly Label languageHint = MakeHint("");
        readonly Label usage = MakeHint("");

        readonly ToggleRow startup = new ToggleRow("Start with Windows",
            "Yappity opens quietly in your tray when you sign in, so the shortcut always works.");
        readonly InputBox vocabInput = new InputBox(true);
        readonly Label about = MakeHint("");

        readonly Timer saveTimer = new Timer();
        int shortcutVk, shortcutMods;
        bool capturing;
        bool loading;
        bool laying;

        public SettingsView(App app)
        {
            this.app = app;
            BackColor = Theme.Bg;
            AutoScroll = true;
            DoubleBuffered = true;

            // Groq key
            keyInput.Box.UseSystemPasswordChar = true;
            keyInput.Cue("gsk_...");
            showBtn.Click += delegate
            {
                keyInput.Box.UseSystemPasswordChar = !keyInput.Box.UseSystemPasswordChar;
                showBtn.Text = keyInput.Box.UseSystemPasswordChar ? "Show" : "Hide";
            };
            testBtn.Click += async delegate
            {
                testBtn.Enabled = false;
                SetStatus("Checking your key with Groq", Theme.Muted);
                try
                {
                    await Groq.CheckKey(keyInput.Box.Text.Trim());
                    SetStatus("Your key works, so you're good to go.", Theme.Success);
                }
                catch (GroqException ex) { SetStatus(ex.Message, Theme.Danger); }
                testBtn.Enabled = true;
            };
            keyLink.Text = "Don't have one yet? You can grab a free key at console.groq.com/keys in about a minute.";
            keyLink.LinkArea = new LinkArea(keyLink.Text.IndexOf("console"), "console.groq.com/keys".Length);
            keyLink.Font = Theme.F(9f);
            keyLink.ForeColor = Theme.Muted;
            keyLink.LinkColor = Theme.Accent;
            keyLink.ActiveLinkColor = Theme.AccentHover;
            keyLink.LinkBehavior = LinkBehavior.HoverUnderline;
            keyLink.LinkClicked += delegate { Open("https://console.groq.com/keys"); };
            var keyRow = new Panel();
            keyRow.Height = Theme.S(34);
            keyRow.Controls.AddRange(new Control[] { keyInput, showBtn, testBtn });
            keyRow.Layout += delegate
            {
                testBtn.SetBounds(keyRow.Width - testBtn.Width, 0, testBtn.Width, Theme.S(34));
                showBtn.SetBounds(testBtn.Left - Theme.S(8) - showBtn.Width, 0, showBtn.Width, Theme.S(34));
                keyInput.SetBounds(0, 0, showBtn.Left - Theme.S(8), Theme.S(34));
            };

            // Shortcut
            shortcutBtn.Click += delegate { StartCapture(); };
            mode.SetChoices(Modes);
            output.SetChoices(Outputs);
            speech.SetChoices(Groq.SpeechModels);
            cleanup.SetChoices(Groq.CleanupModels);
            language.SetChoices(Groq.Languages);

            vocabInput.Height = Theme.S(84);
            vocabInput.Cue("Yappity, Groq, Barestack");

            Heading("Groq API key", 0);
            Add(null, null, null, keyRow);
            Add(null, null, null, keyLink);
            Add(null, null, null, keyStatus);

            Heading("Shortcut", 18);
            Add("Shortcut", shortcutBtn, shortcutHint, null);
            Add("Mode", mode, modeHint, null);
            Add("When you're done", output, outputHint, null);

            Heading("Models", 18);
            Add("Speech to text", speech, speechHint, null);
            Add("Cleanup", cleanup, cleanupHint, null);
            Add("Language", language, languageHint, null);
            Add(null, null, null, usage);

            Heading("Words to spell right", 18);
            Add(null, null, null, vocabInput);
            Add(null, null, null, MakeHint("Add names, brands or slang that Whisper keeps getting wrong, separated by commas. Whisper treats them as spelling hints, so they show up the way you write them."));

            Heading("Other", 18);
            Add(null, null, null, startup);
            Add(null, null, null, about);

            about.Text = "Yappity " + App.Version + ". Your audio goes straight from your PC to Groq and nowhere else, and your key stays encrypted on this PC.";

            Load();

            saveTimer.Interval = 500;
            saveTimer.Tick += delegate { saveTimer.Stop(); Save(); };
            keyInput.Box.TextChanged += delegate { Changed(); };
            vocabInput.Box.TextChanged += delegate { Changed(); };
            foreach (var c in new[] { mode, output, speech, cleanup, language })
                c.Changed += delegate { UpdateHints(); Changed(); };
            startup.Changed += delegate
            {
                try { Store.StartsWithWindows = startup.Checked; }
                catch (Exception e) { Store.Log("Startup setting failed: " + e.Message); }
            };
            VisibleChanged += delegate { if (Visible) UpdateUsage(); else StopCapture(); };
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            Theme.StyleScrollbars(this);
        }

        protected override void OnHandleDestroyed(EventArgs e)
        {
            StopCapture();
            base.OnHandleDestroyed(e);
        }

        public void FocusFirst()
        {
            if (string.IsNullOrEmpty(keyInput.Box.Text)) keyInput.Box.Focus();
        }

        // ---- Building rows ----

        void Heading(string text, int gap)
        {
            var l = new Label();
            l.Text = text.ToUpperInvariant();
            l.Font = Theme.Bold(8.5f);
            l.ForeColor = Theme.Muted;
            l.AutoSize = false;
            l.TextAlign = ContentAlignment.BottomLeft;
            l.Height = Theme.S(26);
            var r = new Row();
            r.Full = l;
            r.Gap = gap;
            rows.Add(r);
            Controls.Add(l);
        }

        void Add(string title, Control right, Label hint, Control full)
        {
            var r = new Row();
            if (title != null)
            {
                r.Title = new Label();
                r.Title.Text = title;
                r.Title.Font = Theme.F(10f);
                r.Title.ForeColor = Theme.Text;
                r.Title.AutoSize = false;
                r.Title.TextAlign = ContentAlignment.MiddleLeft;
                Controls.Add(r.Title);
            }
            r.Right = right;
            r.Hint = hint;
            r.Full = full;
            if (right != null) Controls.Add(right);
            if (hint != null) Controls.Add(hint);
            if (full != null) Controls.Add(full);
            rows.Add(r);
        }

        static Label MakeHint(string text)
        {
            var l = new Label();
            l.Text = text;
            l.Font = Theme.F(9f);
            l.ForeColor = Theme.Muted;
            l.AutoSize = false;
            return l;
        }

        static int TextHeight(Control c, int width)
        {
            if (string.IsNullOrEmpty(c.Text)) return 0;
            return TextRenderer.MeasureText(c.Text, c.Font, new Size(width, int.MaxValue),
                TextFormatFlags.WordBreak | TextFormatFlags.TextBoxControl).Height + Theme.S(2);
        }

        // One column of rows that adapts to the window width.
        protected override void OnLayout(LayoutEventArgs e)
        {
            base.OnLayout(e);
            if (laying) return;
            laying = true;
            try
            {
                int x = Theme.S(24);
                int w = Math.Max(Theme.S(300), ClientSize.Width - x * 2);
                int y = Theme.S(8) + AutoScrollPosition.Y;
                foreach (var r in rows)
                {
                    y += Theme.S(r.Gap);
                    if (r.Full != null)
                    {
                        int h;
                        if (r.Full is ToggleRow) h = ((ToggleRow)r.Full).MeasureHeight(w);
                        else if (r.Full is Label || r.Full is LinkLabel) h = TextHeight(r.Full, w);
                        else h = r.Full.Height;
                        r.Full.SetBounds(x, y, w, h);
                        y += h + (h > 0 ? Theme.S(8) : 0);
                        continue;
                    }
                    int rowH = Theme.S(46);
                    int rw = r.Right.Width;
                    r.Title.SetBounds(x, y, w - rw - Theme.S(12), rowH);
                    r.Right.SetBounds(x + w - rw, y + (rowH - Theme.S(34)) / 2, rw, Theme.S(34));
                    y += rowH;
                    if (r.Hint != null)
                    {
                        int hh = TextHeight(r.Hint, w);
                        r.Hint.SetBounds(x, y - Theme.S(4), w, hh);
                        y += hh + Theme.S(10);
                    }
                }
                AutoScrollMinSize = new Size(0, y - AutoScrollPosition.Y + Theme.S(16));
            }
            finally { laying = false; }
        }

        protected override void OnResize(EventArgs eventargs)
        {
            base.OnResize(eventargs);
            PerformLayout();
            Invalidate(true);
        }

        // ---- Shortcut recording ----

        void StartCapture()
        {
            if (capturing) { StopCapture(); return; }
            capturing = true;
            shortcutBtn.Text = "Press your shortcut";
            shortcutBtn.Primary = true;
            shortcutBtn.Fit();
            shortcutHint.Text = "Press the keys you want to use, all at once. Press Esc to keep the one you have now.";
            shortcutHint.ForeColor = Theme.Text;
            PerformLayout();
            app.CaptureShortcut(delegate(int vk, int mods)
            {
                if (!capturing) return;
                capturing = false;
                string problem = KeyboardHook.Problem(vk, mods);
                if (vk >= 0 && problem == null)
                {
                    shortcutVk = vk;
                    shortcutMods = mods;
                    Changed();
                }
                ShowShortcut(problem);
            });
        }

        void StopCapture()
        {
            if (!capturing) return;
            capturing = false;
            app.CancelCapture();
            ShowShortcut(null);
        }

        void ShowShortcut(string problem)
        {
            shortcutBtn.Primary = false;
            shortcutBtn.Text = KeyboardHook.Describe(shortcutVk, shortcutMods);
            shortcutBtn.Fit();
            if (problem != null)
            {
                shortcutHint.Text = problem;
                shortcutHint.ForeColor = Theme.Danger;
            }
            else
            {
                shortcutHint.Text = "Click it and press any keys to change it. Ctrl + Shift + K is the default.";
                shortcutHint.ForeColor = Theme.Muted;
            }
            PerformLayout();
            Invalidate(true);
        }

        // ---- Loading and saving ----

        void SetStatus(string text, Color color)
        {
            keyStatus.Text = text;
            keyStatus.ForeColor = color;
            PerformLayout();
        }

        void UpdateHints()
        {
            modeHint.Text = mode.Selected.About;
            outputHint.Text = output.Selected.About;
            speechHint.Text = speech.Selected.About;
            cleanupHint.Text = cleanup.Selected.About;
            languageHint.Text = language.Selected.About;
            PerformLayout();
            Invalidate(true);
        }

        public void UpdateUsage()
        {
            int a = Groq.Remaining(speech.Value);
            int b = cleanup.Value.Length > 0 ? Groq.Remaining(cleanup.Value) : -1;
            if (a < 0 && b < 0)
                usage.Text = "After your first yap, this line shows how many recordings and cleanups your key has left today.";
            else
                usage.Text = "Left on your key today: " +
                    (a >= 0 ? a.ToString("N0") + " recordings" : "") +
                    (a >= 0 && b >= 0 ? " and " : "") +
                    (b >= 0 ? b.ToString("N0") + " cleanups" : "") + ". Groq resets these every day.";
            PerformLayout();
        }

        void Load()
        {
            loading = true;
            var s = app.Settings;
            keyInput.Box.Text = s.ApiKey;
            shortcutVk = s.ShortcutVk;
            shortcutMods = s.ShortcutMods;
            ShowShortcut(null);
            mode.Value = s.Mode;
            output.Value = s.Output;
            speech.Value = s.SpeechModel;
            cleanup.Value = s.CleanupModel;
            language.Value = s.Language;
            vocabInput.Box.Text = s.Vocabulary;
            try { startup.Checked = Store.StartsWithWindows; } catch (Exception) { }
            if (string.IsNullOrEmpty(s.ApiKey))
                SetStatus("Paste your key in the box above. It gets encrypted and stays on this PC.", Theme.Muted);
            UpdateHints();
            UpdateUsage();
            loading = false;
        }

        void Changed()
        {
            if (loading) return;
            saveTimer.Stop();
            saveTimer.Start();
        }

        void Save()
        {
            var s = app.Settings;
            bool keyChanged = s.ApiKey != keyInput.Box.Text.Trim();
            s.ApiKey = keyInput.Box.Text;
            s.ShortcutVk = shortcutVk;
            s.ShortcutMods = shortcutMods;
            s.Mode = mode.Value;
            s.Output = output.Value;
            s.SpeechModel = speech.Value;
            s.CleanupModel = cleanup.Value;
            s.Language = language.Value;
            s.Vocabulary = vocabInput.Box.Text.Trim();
            app.ApplySettings();
            if (keyChanged)
                SetStatus(string.IsNullOrEmpty(s.ApiKey) ? "Your key was removed." : "Saved. Press Test to make sure Groq accepts it.", Theme.Muted);
            UpdateUsage();
            var form = FindForm() as MainForm;
            if (form != null) form.ReloadHistory();
        }

        static void Open(string url)
        {
            try { Process.Start(new ProcessStartInfo(url) { UseShellExecute = true }); } catch (Exception) { }
        }
    }
}
