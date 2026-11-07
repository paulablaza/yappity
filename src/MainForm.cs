using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace Saywrite
{
    // History and Settings. Built only when opened and destroyed on close, so it
    // uses no memory while Saywrite sits in the tray.
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
            Text = "Saywrite";
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
            TextRenderer.DrawText(g, "Saywrite", titleFont,
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
            history.HotkeyName = KeyboardHook.Find(app.Settings.HotkeyVk).Name;
            history.SetItems(app.History);
            int n = app.History.Count;
            footerText.Text = "Hold " + history.HotkeyName + " to talk" + (n > 0 ? "  ·  " + n + (n == 1 ? " dictation" : " dictations") : "");
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
                history.Toast("Sending again...");
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
            if (MessageBox.Show(this, "Delete all " + app.History.Count + " dictations from history?", "Saywrite",
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
        readonly App app;
        readonly Label keyHeading = Heading("Groq API key");
        readonly InputBox keyInput = new InputBox(false);
        readonly FlatButton showBtn = new FlatButton("Show", false);
        readonly FlatButton testBtn = new FlatButton("Test", false);
        readonly LinkLabel keyLink = new LinkLabel();
        readonly Label keyStatus = new Label();

        readonly Label dictHeading = Heading("Dictation");
        readonly Label hotkeyLabel = RowLabel("Hold to talk");
        readonly FlatButton hotkeyBtn = new FlatButton("Right Ctrl", false);
        readonly Label langLabel = RowLabel("Language");
        readonly InputBox langInput = new InputBox(false);
        readonly ToggleRow cleanup = new ToggleRow("Clean up with AI", "Removes filler words like um and you know, and fixes punctuation. Keeps your wording.");
        readonly ToggleRow autoPaste = new ToggleRow("Paste automatically", "Types the text where your cursor is. When off, it's copied so you can paste it yourself.");
        readonly ToggleRow restore = new ToggleRow("Keep my clipboard", "Puts back whatever you had copied before Saywrite pasted.");
        readonly ToggleRow startup = new ToggleRow("Start with Windows", null);

        readonly Label vocabHeading = Heading("Words to spell right");
        readonly InputBox vocabInput = new InputBox(true);
        readonly Label vocabHint = Hint("Names, brands or jargon, separated by commas. Whisper uses them as spelling hints.");
        readonly Label about = Hint("");

        readonly Timer saveTimer = new Timer();
        bool loading;
        bool laying;

        public SettingsView(App app)
        {
            this.app = app;
            BackColor = Theme.Bg;
            AutoScroll = true;
            DoubleBuffered = true;

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
                SetStatus("Checking...", Theme.Muted);
                try
                {
                    await Groq.CheckKey(keyInput.Box.Text.Trim());
                    SetStatus("Key works. You're ready to dictate.", Theme.Success);
                }
                catch (GroqException ex) { SetStatus(ex.Message, Theme.Danger); }
                testBtn.Enabled = true;
            };
            keyLink.Text = "Get a free key at console.groq.com/keys";
            keyLink.LinkArea = new LinkArea(keyLink.Text.IndexOf("console"), "console.groq.com/keys".Length);
            keyLink.Font = Theme.F(9f);
            keyLink.ForeColor = Theme.Muted;
            keyLink.LinkColor = Theme.Accent;
            keyLink.ActiveLinkColor = Theme.AccentHover;
            keyLink.LinkBehavior = LinkBehavior.HoverUnderline;
            keyLink.LinkClicked += delegate { Open("https://console.groq.com/keys"); };
            keyStatus.Font = Theme.F(9f);
            keyStatus.ForeColor = Theme.Muted;

            hotkeyBtn.Chevron = true;
            hotkeyBtn.Click += delegate { ShowHotkeyMenu(); };
            langInput.Cue("Auto");
            langInput.Width = Theme.S(110);

            vocabInput.Height = Theme.S(84);
            vocabInput.Cue("e.g. Saywrite, Groq, Kubernetes");

            Controls.AddRange(new Control[]
            {
                keyHeading, keyInput, showBtn, testBtn, keyLink, keyStatus,
                dictHeading, hotkeyLabel, hotkeyBtn, langLabel, langInput, cleanup, autoPaste, restore, startup,
                vocabHeading, vocabInput, vocabHint, about
            });

            about.Text = "Speech: " + Groq.SpeechModel + "   ·   Cleanup: " + Groq.CleanupModel +
                "\nBoth are on Groq's free plan. Your audio goes only to Groq.   ·   Saywrite " + App.Version;

            Load();

            saveTimer.Interval = 500;
            saveTimer.Tick += delegate { saveTimer.Stop(); Save(); };
            keyInput.Box.TextChanged += delegate { Changed(); };
            langInput.Box.TextChanged += delegate { Changed(); };
            vocabInput.Box.TextChanged += delegate { Changed(); };
            foreach (var t in new[] { cleanup, autoPaste, restore }) t.Changed += delegate { Changed(); };
            startup.Changed += delegate
            {
                try { Store.StartsWithWindows = startup.Checked; }
                catch (Exception e) { Store.Log("Startup setting failed: " + e.Message); }
            };
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            Theme.StyleScrollbars(this);
        }

        public void FocusFirst()
        {
            if (string.IsNullOrEmpty(keyInput.Box.Text)) keyInput.Box.Focus();
        }

        static Label Heading(string text)
        {
            var l = new Label();
            l.Text = text.ToUpperInvariant();
            l.Font = Theme.Bold(8.5f);
            l.ForeColor = Theme.Muted;
            l.AutoSize = false;
            l.TextAlign = ContentAlignment.BottomLeft;
            return l;
        }

        static Label RowLabel(string text)
        {
            var l = new Label();
            l.Text = text;
            l.Font = Theme.F(10f);
            l.ForeColor = Theme.Text;
            l.AutoSize = false;
            l.TextAlign = ContentAlignment.MiddleLeft;
            return l;
        }

        static Label Hint(string text)
        {
            var l = new Label();
            l.Text = text;
            l.Font = Theme.F(9f);
            l.ForeColor = Theme.Muted;
            l.AutoSize = false;
            return l;
        }

        void SetStatus(string text, Color color)
        {
            keyStatus.Text = text;
            keyStatus.ForeColor = color;
        }

        void ShowHotkeyMenu()
        {
            var menu = Theme.Menu();
            foreach (var opt in KeyboardHook.Options)
            {
                var o = opt;
                var item = Theme.Item(o.Name, delegate
                {
                    hotkeyBtn.Text = o.Name;
                    hotkeyBtn.Fit();
                    PerformLayout();
                    Changed();
                });
                item.Checked = o.Name == hotkeyBtn.Text;
                menu.Items.Add(item);
            }
            menu.ShowImageMargin = true;
            menu.Show(hotkeyBtn, new Point(0, hotkeyBtn.Height + Theme.S(4)));
        }

        // Positions everything in one column that adapts to the window width.
        protected override void OnLayout(LayoutEventArgs e)
        {
            base.OnLayout(e);
            if (laying) return;
            laying = true;
            try
            {
                int x = Theme.S(24);
                int w = Math.Max(Theme.S(300), ClientSize.Width - x * 2);
                int y = Theme.S(10) + AutoScrollPosition.Y;
                int headH = Theme.S(30);

                keyHeading.SetBounds(x, y, w, headH); y += headH + Theme.S(8);
                int bw = showBtn.Width + testBtn.Width + Theme.S(16);
                keyInput.SetBounds(x, y, w - bw, Theme.S(34));
                showBtn.SetBounds(x + w - bw + Theme.S(8), y, showBtn.Width, Theme.S(34));
                testBtn.SetBounds(showBtn.Right + Theme.S(8), y, testBtn.Width, Theme.S(34));
                y += Theme.S(34) + Theme.S(8);
                keyLink.SetBounds(x, y, w, Theme.S(20)); y += Theme.S(20);
                keyStatus.SetBounds(x, y, w, Theme.S(22)); y += Theme.S(22) + Theme.S(14);

                dictHeading.SetBounds(x, y, w, headH); y += headH + Theme.S(4);
                int rowH = Theme.S(48);
                hotkeyLabel.SetBounds(x, y, w - hotkeyBtn.Width, rowH);
                hotkeyBtn.SetBounds(x + w - hotkeyBtn.Width - Theme.S(2), y + (rowH - Theme.S(34)) / 2, hotkeyBtn.Width, Theme.S(34));
                y += rowH;
                langLabel.SetBounds(x, y, w - langInput.Width, rowH);
                langInput.SetBounds(x + w - langInput.Width - Theme.S(2), y + (rowH - Theme.S(34)) / 2, langInput.Width, Theme.S(34));
                y += rowH;
                foreach (var t in new[] { cleanup, autoPaste, restore, startup })
                {
                    int h = t.MeasureHeight(w);
                    t.SetBounds(x, y, w, h);
                    y += h;
                }
                y += Theme.S(14);

                vocabHeading.SetBounds(x, y, w, headH); y += headH + Theme.S(8);
                vocabInput.SetBounds(x, y, w, vocabInput.Height); y += vocabInput.Height + Theme.S(6);
                int hintH = TextRenderer.MeasureText(vocabHint.Text, vocabHint.Font, new Size(w, int.MaxValue), TextFormatFlags.WordBreak).Height;
                vocabHint.SetBounds(x, y, w, hintH); y += hintH + Theme.S(26);
                int aboutH = TextRenderer.MeasureText(about.Text, about.Font, new Size(w, int.MaxValue), TextFormatFlags.WordBreak).Height;
                about.SetBounds(x, y, w, aboutH); y += aboutH + Theme.S(20);

                AutoScrollMinSize = new Size(0, y - AutoScrollPosition.Y);
            }
            finally { laying = false; }
        }

        protected override void OnResize(EventArgs eventargs)
        {
            base.OnResize(eventargs);
            PerformLayout();
            Invalidate(true);
        }

        void Load()
        {
            loading = true;
            var s = app.Settings;
            keyInput.Box.Text = s.ApiKey;
            hotkeyBtn.Text = KeyboardHook.Find(s.HotkeyVk).Name;
            hotkeyBtn.Fit();
            langInput.Box.Text = s.Language;
            vocabInput.Box.Text = s.Vocabulary;
            cleanup.Checked = s.Cleanup;
            autoPaste.Checked = s.AutoPaste;
            restore.Checked = s.RestoreClipboard;
            try { startup.Checked = Store.StartsWithWindows; } catch (Exception) { }
            if (string.IsNullOrEmpty(s.ApiKey))
                SetStatus("Paste your key to start. It stays encrypted on this PC.", Theme.Muted);
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
            foreach (var o in KeyboardHook.Options) if (o.Name == hotkeyBtn.Text) s.HotkeyVk = o.Vk;
            s.Language = langInput.Box.Text.Trim();
            s.Vocabulary = vocabInput.Box.Text.Trim();
            s.Cleanup = cleanup.Checked;
            s.AutoPaste = autoPaste.Checked;
            s.RestoreClipboard = restore.Checked;
            app.ApplySettings();
            if (keyChanged)
                SetStatus(string.IsNullOrEmpty(s.ApiKey) ? "No key saved." : "Saved. Press Test to check it.", Theme.Muted);
            var form = FindForm() as MainForm;
            if (form != null) form.ReloadHistory();
        }

        static void Open(string url)
        {
            try { Process.Start(new ProcessStartInfo(url) { UseShellExecute = true }); } catch (Exception) { }
        }
    }
}
