using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Windows.Threading;

namespace Yappity
{
    internal static class Options
    {
        public static readonly Choice[] Modes =
        {
            new Choice("toggle", "Toggle",
                "Press the shortcut once to start talking and press it again when you're done. If you change your mind halfway, press Esc and the recording gets thrown away."),
            new Choice("hold", "Hold",
                "Hold the shortcut down while you talk and let go when you're done. Try not to use Shift on its own here, because Windows turns on Filter Keys when you hold Shift for 8 seconds."),
        };

        public static readonly Choice[] Outputs =
        {
            new Choice("paste", "Paste it",
                "The text lands all at once with Ctrl+V, even if you talked for minutes, and whatever you had copied before gets put back right after. This works in almost every app. If one app ignores it, switch to Type it in."),
            new Choice("type", "Type it in",
                "Yappity types the text letter by letter like a keyboard, so it works in the rare apps that block pasting. It's slower, and anything over 400 characters still gets pasted so no words get lost."),
            new Choice("history", "History only",
                "Nothing gets typed anywhere. Open History and double-click a yap whenever you want to copy it."),
        };

        public static readonly Choice[] Bubbles =
        {
            new Choice("top", "Top center", "Shows up at the top of your screen, like a little notch, so it stays out of the way of whatever you're typing in."),
            new Choice("top-right", "Top right", "Shows up in the top right corner of your screen."),
            new Choice("top-left", "Top left", "Shows up in the top left corner of your screen."),
            new Choice("bottom", "Bottom center", "Shows up just above your taskbar in the middle of the screen."),
            new Choice("bottom-right", "Bottom right", "Shows up just above your taskbar in the bottom right corner."),
            new Choice("bottom-left", "Bottom left", "Shows up just above your taskbar in the bottom left corner."),
        };

        // Quick picks for the shortcut. Id is "vk:mods".
        public static readonly Choice[] Shortcuts =
        {
            new Choice("163:0", "Right Ctrl", ""),
            new Choice("165:0", "Right Alt", ""),
            new Choice("161:0", "Right Shift", ""),
            new Choice("75:3", "Ctrl + Shift + K", ""),
            new Choice("32:1", "Ctrl + Space", ""),
            new Choice("120:0", "F9", ""),
            new Choice("19:0", "Pause", ""),
            new Choice("custom", "Record my own", ""),
        };

        public static readonly Choice[] Styles =
        {
            new Choice("raw", "Raw", "Exactly what Whisper heard, um's and all, with nothing changed. It's also the fastest, since there's no cleanup step, and it doesn't use any of your cleanup limit."),
            new Choice("clean", "Clean up", "Takes out the um, uh and false starts and fixes punctuation. The text comes out as one block, the way you said it."),
            new Choice("format", "Clean up and format", "Same cleanup, then lays it out for reading: when you list things, each one goes on its own line with a dash, and long yaps get split into short paragraphs. Still only your words. Works best with Paste it, since typing can't do line breaks."),
            new Choice("organize", "Organize", "Turns a long ramble into a clear message you can send as is. Every point becomes a full sentence on its own dash line, grouped by topic, and the repeats and \"never mind\" bits get dropped. This one rewords a little, so your exact words are always kept in History."),
        };

        public static readonly Choice[] KeepAudio =
        {
            new Choice("1", "1 day", "Every recording stays in History for a day, so you can play it, send it again or drag it out. Recordings that failed to send stay for 30 days."),
            new Choice("3", "3 days", "Every recording stays in History for 3 days, so you can play it, send it again or drag it out. Recordings that failed to send stay for 30 days."),
            new Choice("7", "7 days", "Every recording stays in History for a week, so you can play it, send it again or drag it out. Recordings that failed to send stay for 30 days."),
            new Choice("30", "30 days", "Every recording stays in History for a month. A minute of talking takes about 2 MB, so this can add up if you yap a lot."),
            new Choice("0", "Don't keep", "Recordings are deleted as soon as the text comes back. Recordings that failed to send are still kept for 30 days so you can retry them."),
        };

        public static readonly Choice[] Themes =
        {
            new Choice("system", "Match Windows", "Yappity follows your Windows setting and switches between light and dark along with the rest of your PC."),
            new Choice("light", "Light", "Always light, no matter what Windows is set to."),
            new Choice("dark", "Dark", "Always dark, no matter what Windows is set to."),
        };
    }

    // History and Settings. Built when opened and thrown away when closed, so it
    // uses no memory while Yappity waits in the tray.
    internal sealed class MainWindow : Window
    {
        readonly App app;
        readonly Segmented tabs;
        readonly Grid body = new Grid();
        readonly HistoryPage history;
        readonly SettingsPage settings;

        public MainWindow(App app)
        {
            this.app = app;
            K.Load();
            tabs = new Segmented("History", "Settings");
            Title = "Yappity";
            Width = 600;
            Height = 720;
            MinWidth = 480;
            MinHeight = 520;
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
            Background = K.B(K.Bg);
            FontFamily = K.Font;
            UseLayoutRounding = true;
            TextOptions.SetTextFormattingMode(this, TextFormattingMode.Display);
            try
            {
                var ico = System.Drawing.Icon.ExtractAssociatedIcon(System.Windows.Forms.Application.ExecutablePath);
                Icon = Imaging.CreateBitmapSourceFromHIcon(ico.Handle, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
            }
            catch (Exception) { }

            history = new HistoryPage(app);
            settings = new SettingsPage(app);

            var root = new DockPanel();
            var header = new Grid { Margin = new Thickness(24, 18, 24, 14) };
            var brand = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
            // Yappy-chan's wink next to the name.
            var logo = new Image { Source = K.Image("logo.png"), Height = 40, Margin = new Thickness(0, -4, 10, -2) };
            RenderOptions.SetBitmapScalingMode(logo, BitmapScalingMode.HighQuality);
            brand.Children.Add(logo);
            brand.Children.Add(K.T("Yappity", 21, K.Text, FontWeights.Bold));
            header.Children.Add(brand);
            tabs.VerticalAlignment = VerticalAlignment.Center;
            tabs.Selected += delegate(int i) { Switch(i == 1); };
            var right = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Center };
            right.Children.Add(ThemeButton());
            right.Children.Add(tabs);
            header.Children.Add(right);
            DockPanel.SetDock(header, Dock.Top);
            root.Children.Add(header);

            body.Children.Add(history);
            body.Children.Add(settings);
            root.Children.Add(body);
            Content = root;

            SourceInitialized += delegate { StyleTitleBar(); };
            Closed += delegate { settings.StopCapture(); history.StopAudio(); };
        }

        // One click switches between light and dark. The icon shows what you'll get.
        FrameworkElement ThemeButton()
        {
            var bg = K.B(Colors.Transparent);
            var b = new Border { Width = 36, Height = 36, CornerRadius = new CornerRadius(18), Background = bg, Margin = new Thickness(0, 0, 10, 0), Cursor = Cursors.Hand, ToolTip = K.Dark ? "Switch to light mode" : "Switch to dark mode" };
            K.Hover(b, bg, Colors.Transparent, K.Dark ? K.Hex(0x25252C) : K.Hex(0xE6E6EB));
            var icon = new System.Windows.Shapes.Path { Width = 17, Height = 17, Stretch = Stretch.Uniform, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
            if (K.Dark)
            {
                // Sun
                var g = new GeometryGroup();
                g.Children.Add(new EllipseGeometry(new Point(12, 12), 4.5, 4.5));
                for (int i = 0; i < 8; i++)
                {
                    double a = i * Math.PI / 4;
                    g.Children.Add(new LineGeometry(new Point(12 + Math.Cos(a) * 7.5, 12 + Math.Sin(a) * 7.5), new Point(12 + Math.Cos(a) * 10, 12 + Math.Sin(a) * 10)));
                }
                icon.Data = g;
                icon.Stroke = K.B(K.Text);
                icon.StrokeThickness = 1.8;
                icon.StrokeStartLineCap = PenLineCap.Round;
                icon.StrokeEndLineCap = PenLineCap.Round;
            }
            else
            {
                // Moon
                icon.Data = new CombinedGeometry(GeometryCombineMode.Exclude, new EllipseGeometry(new Point(12, 12), 9, 9), new EllipseGeometry(new Point(17, 8), 7.5, 7.5));
                icon.Fill = K.B(K.Text);
            }
            b.Child = icon;
            var spin = new RotateTransform();
            icon.RenderTransform = spin;
            icon.RenderTransformOrigin = new Point(0.5, 0.5);
            b.MouseLeftButtonUp += delegate
            {
                app.Settings.Theme = K.Dark ? "light" : "dark";
                Store.SaveSettings(app.Settings);
                app.ApplyTheme(Content is FrameworkElement && settings.Visibility == Visibility.Visible);
            };
            return b;
        }

        public void ShowTab(bool showSettings)
        {
            tabs.Select(showSettings ? 1 : 0, IsVisible);
            Switch(showSettings);
            if (!IsVisible) Show();
            if (WindowState == WindowState.Minimized) WindowState = WindowState.Normal;
            Activate();
        }

        void Switch(bool showSettings)
        {
            FrameworkElement show = showSettings ? (FrameworkElement)settings : history;
            FrameworkElement hide = showSettings ? (FrameworkElement)history : settings;
            hide.Visibility = Visibility.Collapsed;
            if (show.Visibility != Visibility.Visible)
            {
                show.Visibility = Visibility.Visible;
                K.Rise(show, 0);
            }
            if (showSettings) settings.Opened(); else settings.StopCapture();
        }

        public void ReloadHistory() { history.Reload(); }

        [DllImport("dwmapi.dll")] static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int value, int size);

        // Dark or light title bar that blends into the window (Windows 11 colors it to match).
        void StyleTitleBar()
        {
            try
            {
                IntPtr h = new WindowInteropHelper(this).Handle;
                int dark = K.Dark ? 1 : 0;
                DwmSetWindowAttribute(h, 20, ref dark, 4);
                int color = K.Bg.R | (K.Bg.G << 8) | (K.Bg.B << 16);
                DwmSetWindowAttribute(h, 35, ref color, 4);  // caption color
                DwmSetWindowAttribute(h, 34, ref color, 4);  // border color
                int round = 2;
                DwmSetWindowAttribute(h, 33, ref round, 4);
            }
            catch (Exception) { }
        }
    }

    // A scroll area with a thin indicator that only shows while scrolling.
    internal class Scroller : Grid
    {
        protected readonly ScrollViewer Viewer = new ScrollViewer();
        readonly Border thumb = new Border { Width = 4, CornerRadius = new CornerRadius(2), HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Top, Margin = new Thickness(0, 0, 5, 0), Opacity = 0, IsHitTestVisible = false };
        readonly DispatcherTimer fade = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(900) };

        public Scroller()
        {
            Viewer.VerticalScrollBarVisibility = ScrollBarVisibility.Hidden;
            Viewer.HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled;
            Viewer.FocusVisualStyle = null;
            thumb.Background = K.B(K.Alpha(K.Muted, 140));
            Children.Add(Viewer);
            Children.Add(thumb);
            Viewer.ScrollChanged += delegate
            {
                if (Viewer.ExtentHeight <= Viewer.ViewportHeight + 1) { thumb.Opacity = 0; return; }
                double h = Math.Max(30, Viewer.ViewportHeight * Viewer.ViewportHeight / Viewer.ExtentHeight);
                thumb.Height = h;
                thumb.Margin = new Thickness(0, Viewer.VerticalOffset / (Viewer.ExtentHeight - Viewer.ViewportHeight) * (Viewer.ViewportHeight - h), 5, 0);
                thumb.BeginAnimation(OpacityProperty, null);
                thumb.Opacity = 1;
                fade.Stop();
                fade.Start();
            };
            fade.Tick += delegate { fade.Stop(); K.To(thumb, OpacityProperty, 0, 400, K.Out); };
        }
    }

    internal sealed class HistoryPage : Grid
    {
        readonly App app;
        readonly Scroller scroller = new Scroller();
        readonly StackPanel list = new StackPanel { Margin = new Thickness(24, 2, 24, 16) };
        readonly TextBlock footer;
        readonly TextBlock clear;
        readonly Border toast = new Border { CornerRadius = new CornerRadius(17), Padding = new Thickness(16, 8, 16, 9), HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Bottom, Margin = new Thickness(0, 0, 0, 62), Opacity = 0, IsHitTestVisible = false };
        readonly DispatcherTimer toastTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(1300) };
        readonly DispatcherTimer clearTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(3) };
        string openId;
        bool armed;
        bool first = true;

        public HistoryPage(App app)
        {
            this.app = app;
            RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            var host = (ScrollViewer)scroller.Children[0];
            host.Content = list;
            Children.Add(scroller);

            var bar = new Grid { Margin = new Thickness(28, 10, 28, 16) };
            footer = K.T("", 12.5, K.Muted, FontWeights.Normal);
            clear = K.T("Clear all", 12.5, K.Muted, FontWeights.Medium);
            clear.HorizontalAlignment = HorizontalAlignment.Right;
            clear.Cursor = Cursors.Hand;
            clear.MouseEnter += delegate { clear.Foreground = K.B(K.Danger); };
            clear.MouseLeave += delegate { if (!armed) clear.Foreground = K.B(K.Muted); };
            clear.MouseLeftButtonUp += delegate { ClearAll(); };
            bar.Children.Add(footer);
            bar.Children.Add(clear);
            Grid.SetRow(bar, 1);
            Children.Add(bar);

            toast.Background = K.B(K.Text);
            toast.Child = K.T("", 13, K.Bg, FontWeights.SemiBold);
            toast.RenderTransform = new TranslateTransform();
            Children.Add(toast);
            toastTimer.Tick += delegate
            {
                toastTimer.Stop();
                K.To(toast, OpacityProperty, 0, 250, K.Out);
            };
            clearTimer.Tick += delegate { clearTimer.Stop(); armed = false; clear.Text = "Clear all"; clear.Foreground = K.B(K.Muted); };
            // For screenshots: YAPPITY_OPEN_FIRST=1 opens the newest card.
            if (Environment.GetEnvironmentVariable("YAPPITY_OPEN_FIRST") == "1" && app.History.Count > 0) openId = app.History[0].Id;
            Reload();
        }

        public void Reload()
        {
            list.Children.Clear();
            int n = app.History.Count;
            if (n == 0) list.Children.Add(Empty());
            int i = 0;
            foreach (var e in app.History)
            {
                var card = Card(e);
                list.Children.Add(card);
                if (first && i < 12) K.Rise(card, i * 35);
                i++;
            }
            first = false;
            footer.Text = n == 0 ? "" : n + (n == 1 ? " yap" : " yaps") + "  ·  double-click one to copy it";
            clear.Visibility = n > 0 ? Visibility.Visible : Visibility.Collapsed;
        }

        FrameworkElement Empty()
        {
            var p = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(20, 40, 20, 20) };
            if (K.Image("dance-0.png") != null)
            {
                // She dances while she waits for your first yap.
                var pic = new Image { Height = 190, Margin = new Thickness(0, 0, 0, 18) };
                RenderOptions.SetBitmapScalingMode(pic, BitmapScalingMode.HighQuality);
                new FrameLoop(pic).Play("dance", 170, false);
                p.Children.Add(pic);
                // She floats up and down a little while she waits.
                var bob = new TranslateTransform();
                pic.RenderTransform = bob;
                var a = new DoubleAnimation(0, -6, TimeSpan.FromSeconds(1.6)) { AutoReverse = true, RepeatBehavior = RepeatBehavior.Forever, EasingFunction = new SineEase { EasingMode = EasingMode.EaseInOut } };
                bob.BeginAnimation(TranslateTransform.YProperty, a);
            }
            var title = K.T("No yaps yet", 20, K.Text, FontWeights.Bold);
            title.HorizontalAlignment = HorizontalAlignment.Center;
            p.Children.Add(title);
            var text = K.Wrap(app.HowTo() + " Whatever you say gets typed where your cursor is, and it shows up here too.", 13.5, K.Muted);
            text.TextAlignment = TextAlignment.Center;
            text.MaxWidth = 380;
            text.Margin = new Thickness(0, 8, 0, 0);
            p.Children.Add(text);
            return p;
        }

        FrameworkElement Card(HistoryEntry e)
        {
            var bg = K.B(K.Surface);
            var card = new Border
            {
                Background = bg,
                CornerRadius = new CornerRadius(14),
                BorderBrush = K.B(e.Id == openId ? K.Accent : (K.Dark ? K.Line : K.Alpha(K.Line, 170))),
                BorderThickness = new Thickness(e.Id == openId ? 1.5 : 1),
                Padding = new Thickness(16, 12, 16, 14),
                Margin = new Thickness(0, 0, 0, 10),
                Cursor = Cursors.Hand,
            };
            if (!K.Dark) card.Effect = K.Shadow(12, 0.05);
            K.Hover(card, bg, K.Surface, K.Dark ? K.Hex(0x212127) : K.Hex(0xFAFAFC));

            var stack = new StackPanel();
            var meta = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 5) };
            var local = e.TimeUtc.ToLocalTime();
            string when = local.Date == DateTime.Today ? local.ToString("h:mm tt") :
                local.Date == DateTime.Today.AddDays(-1) ? "Yesterday, " + local.ToString("h:mm tt") : local.ToString("MMM d, h:mm tt");
            if (e.Seconds > 0) when += "  ·  " + (e.Seconds < 60 ? Math.Max(1, (int)Math.Round(e.Seconds)) + "s" : ((int)e.Seconds / 60) + "m " + ((int)e.Seconds % 60) + "s");
            meta.Children.Add(K.T(when, 12, K.Muted, FontWeights.Normal));
            if (e.Failed)
            {
                var badge = new Border { Background = K.B(K.Alpha(K.Warn, (byte)(K.Dark ? 45 : 30))), CornerRadius = new CornerRadius(8), Padding = new Thickness(7, 1, 7, 2), Margin = new Thickness(8, 0, 0, 0) };
                badge.Child = K.T("Didn't send", 11.5, K.Warn, FontWeights.SemiBold);
                meta.Children.Add(badge);
            }
            stack.Children.Add(meta);

            bool open = e.Id == openId;
            var text = K.Wrap(e.Failed ? (e.Error ?? "Failed") : (e.Text ?? ""), 14.5, e.Failed ? K.Muted : K.Text);
            if (!open)
            {
                text.MaxHeight = text.LineHeight * 3 + 1;
                text.TextTrimming = TextTrimming.CharacterEllipsis;
            }
            stack.Children.Add(text);

            if (open)
            {
                var actions = new WrapPanel { Margin = new Thickness(0, 12, 0, 0) };
                if (!e.Failed)
                {
                    actions.Children.Add(Action("Copy", K.Accent, delegate { Copy(e.Text); }));
                    if (e.Raw != e.Text) actions.Children.Add(Action("Copy original", K.Accent, delegate { Copy(e.Raw); }));
                }
                if (HasAudio(e))
                {
                    actions.Children.Add(AudioChip(e));
                    actions.Children.Add(Action(e.Failed ? "Send again" : "Redo", K.Accent, delegate { Toast("Sending it again"); app.Retry(e); }));
                    actions.Children.Add(Action("Show file", K.Accent, delegate
                    {
                        try { Process.Start("explorer.exe", "/select,\"" + e.AudioFile + "\""); } catch (Exception) { }
                    }));
                }
                actions.Children.Add(Action("Delete", K.Muted, delegate
                {
                    StopAudio();
                    Store.DeleteAudio(e);
                    app.History.Remove(e);
                    Store.SaveHistory(app.History);
                    Reload();
                }));
                stack.Children.Add(actions);
                if (HasAudio(e))
                {
                    var tip = K.Wrap("Drag the recording into any app or folder to keep it. " +
                        (e.Failed ? "It stays here for 30 days so you can send it again." : "Old recordings get deleted on their own, you can change that in Settings."), 12, K.Muted);
                    tip.Margin = new Thickness(0, 8, 0, 0);
                    stack.Children.Add(tip);
                }
                if (!e.Failed && e.Raw != e.Text)
                {
                    var orig = K.Wrap("What Whisper heard: " + e.Raw, 12.5, K.Muted);
                    orig.Margin = new Thickness(0, 10, 0, 0);
                    stack.Children.Insert(2, orig);
                }
            }
            card.Child = stack;

            card.MouseLeftButtonDown += delegate(object s, MouseButtonEventArgs ev)
            {
                if (ev.ClickCount == 2 && !e.Failed) { Copy(e.Text); ev.Handled = true; }
            };
            card.MouseLeftButtonUp += delegate(object s, MouseButtonEventArgs ev)
            {
                if (ev.OriginalSource is TextBlock && ((TextBlock)ev.OriginalSource).Tag as string == "action") return;
                openId = openId == e.Id ? null : e.Id;
                Reload();
            };
            return card;
        }

        static bool HasAudio(HistoryEntry e)
        {
            return !string.IsNullOrEmpty(e.AudioFile) && System.IO.File.Exists(e.AudioFile);
        }

        // ---- Playing and dragging recordings ----

        System.Media.SoundPlayer player;
        string playingId;
        readonly DispatcherTimer playTimer = new DispatcherTimer();

        // A small pill: click to play or stop, drag it out to copy the .wav file anywhere.
        FrameworkElement AudioChip(HistoryEntry e)
        {
            bool playing = playingId == e.Id;
            var bg = K.B(playing ? K.Accent : K.AccentSoft);
            var chip = new Border { Background = bg, CornerRadius = new CornerRadius(12), Padding = new Thickness(10, 3, 12, 4), Margin = new Thickness(0, 0, 18, 6), Cursor = Cursors.Hand, Tag = "action" };
            var row = new StackPanel { Orientation = Orientation.Horizontal };
            var icon = new System.Windows.Shapes.Path
            {
                Data = Geometry.Parse(playing ? "M0,0 L8,0 L8,8 L0,8 Z" : "M0,0 L8,4.5 L0,9 Z"),
                Fill = K.B(playing ? Colors.White : K.Accent),
                Width = 9, Height = 9, Stretch = Stretch.Uniform, VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 7, 0), Tag = "action",
            };
            row.Children.Add(icon);
            string len = e.Seconds < 60 ? "0:" + ((int)Math.Round(e.Seconds)).ToString("00") : ((int)e.Seconds / 60) + ":" + ((int)e.Seconds % 60).ToString("00");
            var t = K.T(playing ? "Stop" : "Play  " + len, 12.5, playing ? Colors.White : K.Accent, FontWeights.SemiBold);
            t.Tag = "action";
            row.Children.Add(t);
            chip.Child = row;

            Point down = new Point();
            bool pressed = false;
            chip.PreviewMouseLeftButtonDown += delegate(object s, MouseButtonEventArgs ev) { down = ev.GetPosition(chip); pressed = true; ev.Handled = true; };
            chip.PreviewMouseMove += delegate(object s, MouseEventArgs ev)
            {
                if (!pressed || ev.LeftButton != MouseButtonState.Pressed) return;
                var p = ev.GetPosition(chip);
                if (Math.Abs(p.X - down.X) < 6 && Math.Abs(p.Y - down.Y) < 6) return;
                pressed = false;
                var data = new DataObject(DataFormats.FileDrop, new[] { e.AudioFile });
                DragDrop.DoDragDrop(chip, data, DragDropEffects.Copy);
            };
            chip.PreviewMouseLeftButtonUp += delegate(object s, MouseButtonEventArgs ev)
            {
                ev.Handled = true;
                if (!pressed) return;
                pressed = false;
                if (playingId == e.Id) StopAudio(); else PlayAudio(e);
                Reload();
            };
            return chip;
        }

        void PlayAudio(HistoryEntry e)
        {
            StopAudio();
            try
            {
                player = new System.Media.SoundPlayer(e.AudioFile);
                player.Play();
                playingId = e.Id;
                playTimer.Stop();
                playTimer.Interval = TimeSpan.FromSeconds(Math.Max(1, e.Seconds) + 0.3);
                playTimer.Tick -= PlayDone;
                playTimer.Tick += PlayDone;
                playTimer.Start();
            }
            catch (Exception ex) { Store.Log("Couldn't play the recording: " + ex.Message); Toast("Couldn't play that one"); }
        }

        void PlayDone(object s, EventArgs ev) { StopAudio(); Reload(); }

        public void StopAudio()
        {
            playTimer.Stop();
            if (player != null) { try { player.Stop(); player.Dispose(); } catch (Exception) { } player = null; }
            playingId = null;
        }

        TextBlock Action(string text, Color color, Action click)
        {
            var t = K.T(text, 13, color, FontWeights.SemiBold);
            t.Tag = "action";
            t.Margin = new Thickness(0, 2, 20, 8);
            t.Cursor = Cursors.Hand;
            t.MouseEnter += delegate { t.Opacity = 0.7; };
            t.MouseLeave += delegate { t.Opacity = 1; };
            t.MouseLeftButtonUp += delegate(object s, MouseButtonEventArgs ev) { ev.Handled = true; click(); };
            return t;
        }

        void Copy(string text)
        {
            if (Paster.SetText(text)) Toast("Copied");
        }

        void Toast(string text)
        {
            ((TextBlock)toast.Child).Text = text;
            var move = (TranslateTransform)toast.RenderTransform;
            move.Y = 8;
            K.To(move, TranslateTransform.YProperty, 0, 300, K.Spring);
            K.To(toast, OpacityProperty, 1, 160, K.Out);
            toastTimer.Stop();
            toastTimer.Start();
        }

        // First click arms it, a second click within 3 seconds clears everything.
        void ClearAll()
        {
            if (!armed)
            {
                armed = true;
                clear.Text = "Click again to delete all " + app.History.Count;
                clear.Foreground = K.B(K.Danger);
                clearTimer.Start();
                return;
            }
            clearTimer.Stop();
            armed = false;
            clear.Text = "Clear all";
            foreach (var e in app.History) Store.DeleteAudio(e);
            app.History.Clear();
            Store.SaveHistory(app.History);
            Reload();
        }
    }

    // Settings save on their own as you change them.
    internal sealed class SettingsPage : Scroller
    {
        readonly App app;
        readonly Field key = new Field("gsk_...", false, true);
        readonly PillButton show = new PillButton("Show", false);
        readonly PillButton test = new PillButton("Test", false);
        readonly TextBlock keyStatus = K.Wrap("", 12.5, K.Muted);
        readonly Border shortcutButton = new Border { Cursor = Cursors.Hand, Padding = new Thickness(2), CornerRadius = new CornerRadius(9) };
        readonly KeyCaps caps = new KeyCaps();
        readonly Picker presets = new Picker();
        readonly TextBlock shortcutHint = K.Wrap("", 12.5, K.Muted);
        readonly Picker mode = new Picker(), output = new Picker(), bubble = new Picker(), speech = new Picker(), cleanup = new Picker(), language = new Picker(), theme = new Picker(), keep = new Picker(), style = new Picker();
        readonly TextBlock styleHint = K.Wrap("", 12.5, K.Muted);
        readonly TextBlock keepHint = K.Wrap("", 12.5, K.Muted);
        readonly TextBlock modeHint = K.Wrap("", 12.5, K.Muted), outputHint = K.Wrap("", 12.5, K.Muted), bubbleHint = K.Wrap("", 12.5, K.Muted);
        readonly TextBlock speechHint = K.Wrap("", 12.5, K.Muted), cleanupHint = K.Wrap("", 12.5, K.Muted), languageHint = K.Wrap("", 12.5, K.Muted), themeHint = K.Wrap("", 12.5, K.Muted);
        readonly TextBlock usage = K.Wrap("", 12.5, K.Muted);
        readonly Field vocab = new Field("Yappity, Groq, Barestack", true, false);
        readonly Toggle startup = new Toggle();
        readonly DispatcherTimer saveTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(500) };
        int shortcutVk, shortcutMods;
        bool capturing, loading;

        public SettingsPage(App app)
        {
            this.app = app;
            Visibility = Visibility.Collapsed;
            var page = new StackPanel { Margin = new Thickness(24, 0, 24, 28), MaxWidth = 620 };
            Viewer.Content = page;

            mode.SetChoices(Options.Modes);
            output.SetChoices(Options.Outputs);
            bubble.SetChoices(Options.Bubbles);
            speech.SetChoices(Groq.SpeechModels);
            cleanup.SetChoices(Groq.CleanupModels);
            language.SetChoices(Groq.Languages);
            theme.SetChoices(Options.Themes);
            keep.SetChoices(Options.KeepAudio);
            style.SetChoices(Options.Styles);

            // Welcome banner when there's no key yet.
            if (string.IsNullOrEmpty(app.Settings.ApiKey)) page.Children.Add(Welcome());

            // Groq key
            page.Children.Add(Heading("Groq API key"));
            var keyGroup = new Group();
            var keyRow = new Grid { Margin = new Thickness(14, 12, 14, 12) };
            keyRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            keyRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            keyRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            keyRow.Children.Add(key);
            show.Margin = new Thickness(8, 0, 0, 0);
            test.Margin = new Thickness(8, 0, 0, 0);
            Grid.SetColumn(show, 1);
            Grid.SetColumn(test, 2);
            keyRow.Children.Add(show);
            keyRow.Children.Add(test);
            keyGroup.Add(keyRow);
            page.Children.Add(keyGroup);
            var link = new TextBlock { TextWrapping = TextWrapping.Wrap, Margin = new Thickness(6, 8, 6, 0), FontFamily = K.Font, FontSize = 12.5, Foreground = K.B(K.Muted) };
            link.Inlines.Add(new System.Windows.Documents.Run("Don't have one yet? You can grab a free key at "));
            var hl = new System.Windows.Documents.Hyperlink(new System.Windows.Documents.Run("console.groq.com/keys")) { Foreground = K.B(K.Accent), TextDecorations = null, Cursor = Cursors.Hand };
            hl.Click += delegate { Open("https://console.groq.com/keys"); };
            link.Inlines.Add(hl);
            link.Inlines.Add(new System.Windows.Documents.Run(" in about a minute."));
            page.Children.Add(link);
            keyStatus.Margin = new Thickness(6, 4, 6, 0);
            page.Children.Add(keyStatus);
            show.Click += delegate { key.Reveal = !key.Reveal; show.Text = key.Reveal ? "Hide" : "Show"; };
            test.Click += async delegate
            {
                test.IsEnabled = false;
                Status("Checking your key with Groq", K.Muted);
                try
                {
                    await Groq.CheckKey(key.Text.Trim());
                    Status("Your key works, so you're good to go.", K.Success);
                }
                catch (GroqException ex) { Status(ex.Message, K.Danger); }
                test.IsEnabled = true;
            };

            // Shortcut
            page.Children.Add(Heading("Shortcut"));
            var g1 = new Group();
            shortcutButton.Child = caps;
            shortcutButton.MouseLeftButtonUp += delegate { if (capturing) StopCapture(); else StartCapture(); };
            presets.SetChoices(Options.Shortcuts);
            presets.Margin = new Thickness(10, 0, 0, 0);
            presets.Changed += delegate
            {
                if (presets.Value == "custom") { StartCapture(); return; }
                var parts = presets.Value.Split(':');
                shortcutVk = int.Parse(parts[0]);
                shortcutMods = int.Parse(parts[1]);
                StopCapture();
                ShowShortcut(null);
                Changed();
            };
            var shortcutRight = new StackPanel { Orientation = Orientation.Horizontal };
            shortcutRight.Children.Add(shortcutButton);
            shortcutRight.Children.Add(presets);
            g1.Add(Group.Row("Shortcut", shortcutHint, shortcutRight));
            g1.Add(Group.Row("Mode", modeHint, mode));
            g1.Add(Group.Row("When you're done", outputHint, output));
            g1.Add(Group.Row("Bubble", bubbleHint, bubble));
            page.Children.Add(g1);

            // Models
            page.Children.Add(Heading("Models"));
            var g2 = new Group();
            g2.Add(Group.Row("Speech to text", speechHint, speech));
            g2.Add(Group.Row("Cleanup", cleanupHint, cleanup));
            g2.Add(Group.Row("Style", styleHint, style));
            g2.Add(Group.Row("Language", languageHint, language));
            page.Children.Add(g2);
            usage.Margin = new Thickness(6, 8, 6, 0);
            page.Children.Add(usage);

            // Words
            page.Children.Add(Heading("Words to spell right"));
            var g3 = new Group();
            vocab.Margin = new Thickness(12);
            vocab.BorderThickness = new Thickness(1);
            g3.Add(vocab);
            page.Children.Add(g3);
            var vocabHint = K.Wrap("Add names, brands or slang that Whisper keeps getting wrong, separated by commas. Whisper treats them as spelling hints, so they show up the way you write them.", 12.5, K.Muted);
            vocabHint.Margin = new Thickness(6, 8, 6, 0);
            page.Children.Add(vocabHint);

            // Other
            page.Children.Add(Heading("Other"));
            var g4 = new Group();
            g4.Add(Group.Row("Keep recordings", keepHint, keep));
            g4.Add(Group.Row("Theme", themeHint, theme));
            g4.Add(InstallRow());
            g4.Add(Group.Row("Start with Windows", K.Wrap("Yappity opens quietly in your tray when you sign in, so the shortcut always works.", 12.5, K.Muted), startup));
            page.Children.Add(g4);

            var about = K.Wrap("Yappity " + App.Version + ". Your audio goes straight from your PC to Groq and nowhere else, and your key stays encrypted on this PC.", 12, K.Muted);
            about.Margin = new Thickness(6, 18, 6, 0);
            about.TextAlignment = TextAlignment.Center;
            page.Children.Add(about);

            Load();

            saveTimer.Tick += delegate { saveTimer.Stop(); Save(); };
            key.TextChanged += delegate { Changed(); };
            vocab.TextChanged += delegate { Changed(); };
            foreach (var p in new[] { mode, output, bubble, speech, cleanup, language, keep, style })
                p.Changed += delegate { Hints(); Changed(); };
            theme.Changed += delegate
            {
                saveTimer.Stop();
                Save();
                app.Settings.Theme = theme.Value;
                Store.SaveSettings(app.Settings);
                app.ApplyTheme();
            };
            startup.Changed += delegate
            {
                try { Store.StartsWithWindows = startup.IsOn; }
                catch (Exception e) { Store.Log("Startup setting failed: " + e.Message); }
            };
        }

        FrameworkElement Welcome()
        {
            var card = new Border { Background = K.B(K.AccentSoft), CornerRadius = new CornerRadius(16), Padding = new Thickness(18, 14, 18, 14), Margin = new Thickness(0, 4, 0, 4) };
            var g = new Grid();
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            if (K.Image("wave-0.png") != null)
            {
                var pic = new Image { Height = 104, Margin = new Thickness(0, -2, 16, -2) };
                RenderOptions.SetBitmapScalingMode(pic, BitmapScalingMode.HighQuality);
                new FrameLoop(pic).Play("wave", 150, true);
                g.Children.Add(pic);
            }
            var text = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            text.Children.Add(K.T("Hi, I'm Yappy-chan!", 17, K.Text, FontWeights.Bold));
            var body = K.Wrap("Paste your free Groq key below and you're ready. " + app.HowTo() + " I'll type it wherever your cursor is.", 13, K.Muted);
            body.Margin = new Thickness(0, 5, 0, 0);
            text.Children.Add(body);
            Grid.SetColumn(text, 1);
            g.Children.Add(text);
            card.Child = g;
            return card;
        }

        FrameworkElement InstallRow()
        {
            if (Installer.IsInstalledCopy)
                return Group.Row("Installed", K.Wrap("Yappity is installed in your user folder and shows up in the Start menu. You can remove it any time from Windows Settings > Apps.", 12.5, K.Muted), null);
            if (Store.IsPortable)
                return Group.Row("Portable mode", K.Wrap("Settings, history and recordings live in the YappityData folder next to Yappity.exe, so you can carry it on a USB stick.", 12.5, K.Muted), null);
            var button = new PillButton(Installer.IsInstalled ? "Update" : "Install", true);
            button.Click += delegate
            {
                try { app.InstallAndRestart(); }
                catch (Exception ex) { Store.Log("Install failed: " + ex); Status("Couldn't install: " + ex.Message, K.Danger); }
            };
            string text = Installer.IsInstalled
                ? "An installed copy is already on this PC. Update it with this version and switch to it."
                : "Right now Yappity runs from wherever you saved it. Installing copies it to your user folder, adds it to the Start menu, and lets you uninstall it from Windows Settings like any other app. No admin needed, and your settings come along.";
            return Group.Row("Install on this PC", K.Wrap(text, 12.5, K.Muted), button);
        }

        static TextBlock Heading(string text)
        {
            var t = K.T(text, 13, K.Muted, FontWeights.SemiBold);
            t.Margin = new Thickness(6, 22, 0, 8);
            return t;
        }

        void Status(string text, Color color)
        {
            keyStatus.Text = text;
            keyStatus.Foreground = K.B(color);
            keyStatus.Visibility = text.Length > 0 ? Visibility.Visible : Visibility.Collapsed;
        }

        public void Opened()
        {
            Usage();
            if (key.Text.Length == 0) key.Box.Focus();
        }

        // ---- Shortcut recording ----

        void StartCapture()
        {
            capturing = true;
            caps.Children.Clear();
            var t = K.T("Press your keys", 13, Colors.White, FontWeights.SemiBold);
            var pill = new Border { Background = K.B(K.Accent), CornerRadius = new CornerRadius(8), Padding = new Thickness(12, 5, 12, 6), Child = t };
            caps.Children.Add(pill);
            var pulse = new DoubleAnimation(1, 0.6, TimeSpan.FromMilliseconds(700)) { AutoReverse = true, RepeatBehavior = RepeatBehavior.Forever };
            pill.BeginAnimation(OpacityProperty, pulse);
            shortcutHint.Text = "Press the keys you want to use, all at once. Press Esc to keep the one you have now.";
            shortcutHint.Foreground = K.B(K.Text);
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

        public void StopCapture()
        {
            if (!capturing) return;
            capturing = false;
            app.CancelCapture();
            ShowShortcut(null);
        }

        void ShowShortcut(string problem)
        {
            caps.Show(KeyboardHook.Describe(shortcutVk, shortcutMods));
            string id = shortcutVk + ":" + shortcutMods;
            bool known = false;
            foreach (var c in Options.Shortcuts) if (c.Id == id) known = true;
            presets.Value = known ? id : "custom";
            if (!known) presets.Text = "Presets";
            if (problem != null)
            {
                shortcutHint.Text = problem;
                shortcutHint.Foreground = K.B(K.Danger);
            }
            else
            {
                shortcutHint.Text = KeyboardHook.IsModifier(shortcutVk) && shortcutMods == 0
                    ? "Tap it to start and tap it again to stop, or hold it in Hold mode. Using it in a normal shortcut, like Ctrl + C, still works. Pick another one from Presets, or click the keys to record your own."
                    : "Pick one from Presets, or click the keys and press your own. Right Ctrl is the default.";
                shortcutHint.Foreground = K.B(K.Muted);
            }
        }

        // ---- Loading and saving ----

        void Hints()
        {
            modeHint.Text = mode.Selected.About;
            outputHint.Text = output.Selected.About;
            bubbleHint.Text = bubble.Selected.About;
            speechHint.Text = speech.Selected.About;
            cleanupHint.Text = cleanup.Selected.About;
            languageHint.Text = language.Selected.About;
            themeHint.Text = theme.Selected.About;
            keepHint.Text = keep.Selected.About;
            styleHint.Text = style.Selected.About;
        }

        void Usage()
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
        }

        void Load()
        {
            loading = true;
            var s = app.Settings;
            key.Text = s.ApiKey;
            shortcutVk = s.ShortcutVk;
            shortcutMods = s.ShortcutMods;
            ShowShortcut(null);
            mode.Value = s.Mode;
            output.Value = s.Output;
            bubble.Value = s.Bubble;
            speech.Value = s.SpeechModel;
            cleanup.Value = s.CleanupModel;
            language.Value = s.Language;
            theme.Value = s.Theme;
            keep.Value = s.KeepAudioDays.ToString();
            style.Value = s.Style;
            vocab.Text = s.Vocabulary;
            try { startup.IsOn = Store.StartsWithWindows; } catch (Exception) { }
            Status(string.IsNullOrEmpty(s.ApiKey) ? "Your key gets encrypted and stays on this PC." : "", K.Muted);
            Hints();
            Usage();
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
            bool keyChanged = s.ApiKey != key.Text.Trim();
            s.ApiKey = key.Text;
            s.ShortcutVk = shortcutVk;
            s.ShortcutMods = shortcutMods;
            s.Mode = mode.Value;
            s.Output = output.Value;
            s.Bubble = bubble.Value;
            s.SpeechModel = speech.Value;
            s.CleanupModel = cleanup.Value;
            s.Language = language.Value;
            s.Vocabulary = vocab.Text.Trim();
            int days;
            if (int.TryParse(keep.Value, out days)) s.KeepAudioDays = days;
            s.Style = style.Value;
            app.ApplySettings();
            app.PruneAudio();
            if (keyChanged)
                Status(string.IsNullOrEmpty(s.ApiKey) ? "Your key was removed." : "Saved. Press Test to make sure Groq accepts it.", K.Muted);
            Usage();
            app.RefreshHistory();
        }

        static void Open(string url)
        {
            try { Process.Start(new ProcessStartInfo(url) { UseShellExecute = true }); } catch (Exception) { }
        }
    }
}
