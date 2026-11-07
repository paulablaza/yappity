using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;
using System.Windows.Threading;

namespace Yappity
{
    // The floating pill with Chatter-Chan standing at its edge. While you talk she
    // moves her mouth with your voice; then she writes, tidies up, and gives a
    // thumbs up. The bubble never takes focus and clicks pass straight through it.
    internal sealed class Bubble : Window
    {
        enum Mode { Hidden, Listening, Working, Message }

        public static readonly Color Green = Color.FromRgb(52, 211, 153);
        public static readonly Color Amber = Color.FromRgb(251, 191, 36);
        public static readonly Color Grey = Color.FromRgb(161, 161, 170);
        static readonly Color Red = Color.FromRgb(255, 69, 58);

        // "top", "top-left", "top-right", "bottom", "bottom-left" or "bottom-right".
        public string Position = "top";

        const int Bars = 14;
        const double FigureHeight = 70;
        readonly Rectangle[] bars = new Rectangle[Bars];
        readonly double[] targets = new double[Bars];
        readonly double[] heights = new double[Bars];
        int barPos;
        double voice;      // smoothed loudness, drives her mouth
        int talkFrame = -1;

        readonly Border pill = new Border();
        readonly ScaleTransform pillScale = new ScaleTransform(1, 1);
        readonly Image figure = new Image { Height = FigureHeight, VerticalAlignment = VerticalAlignment.Bottom };
        readonly TranslateTransform figureMove = new TranslateTransform();
        readonly ScaleTransform figureScale = new ScaleTransform(1, 1);
        readonly Grid status = new Grid { Width = 18, Height = 18, Margin = new Thickness(0, 0, 9, 0), VerticalAlignment = VerticalAlignment.Center };
        readonly StackPanel barPanel = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 12, 0), Height = 22, VerticalAlignment = VerticalAlignment.Center };
        readonly StackPanel dots = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 10, 0) };
        readonly TextBlock label = new TextBlock();
        readonly DispatcherTimer frame = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(33) };
        readonly DispatcherTimer hideTimer = new DispatcherTimer();
        Mode mode = Mode.Hidden;
        string pose;
        DateTime since;
        double tick;

        public Bubble()
        {
            WindowStyle = WindowStyle.None;
            AllowsTransparency = true;
            Background = Brushes.Transparent;
            Topmost = true;
            ShowInTaskbar = false;
            ShowActivated = false;
            ResizeMode = ResizeMode.NoResize;
            SizeToContent = SizeToContent.WidthAndHeight;
            Focusable = false;

            pill.CornerRadius = new CornerRadius(25);
            pill.Height = 50;
            pill.Padding = new Thickness(6, 0, 20, 0);
            pill.Background = new SolidColorBrush(Color.FromArgb(242, 22, 22, 26));
            pill.BorderBrush = new SolidColorBrush(Color.FromArgb(38, 255, 255, 255));
            pill.BorderThickness = new Thickness(1);
            pill.Effect = K.Shadow(26, 0.45);
            // Room above for her head poking out, and around for the shadow.
            pill.Margin = new Thickness(26, 34, 26, 32);
            pill.RenderTransform = pillScale;
            pill.RenderTransformOrigin = new Point(0.5, 0.5);

            // She stands on the bottom edge of the pill and pokes out above it.
            var tg = new TransformGroup();
            tg.Children.Add(figureScale);
            tg.Children.Add(figureMove);
            figure.RenderTransform = tg;
            figure.RenderTransformOrigin = new Point(0.5, 1);
            figure.Margin = new Thickness(0, -(FigureHeight - 46), 6, 2);
            RenderOptions.SetBitmapScalingMode(figure, BitmapScalingMode.HighQuality);

            for (int i = 0; i < Bars; i++)
            {
                bars[i] = new Rectangle { Width = 3, Height = 3, RadiusX = 1.5, RadiusY = 1.5, Fill = Brushes.White, Margin = new Thickness(0, 0, 2.5, 0), VerticalAlignment = VerticalAlignment.Center };
                barPanel.Children.Add(bars[i]);
            }
            for (int i = 0; i < 3; i++)
                dots.Children.Add(new Ellipse { Width = 6, Height = 6, Fill = new SolidColorBrush(Color.FromRgb(167, 155, 255)), Margin = new Thickness(0, 0, 4, 0), RenderTransform = new TranslateTransform() });

            label.FontFamily = K.Font;
            label.FontSize = 13.5;
            label.FontWeight = FontWeights.SemiBold;
            label.Foreground = new SolidColorBrush(Color.FromRgb(245, 245, 247));
            label.VerticalAlignment = VerticalAlignment.Center;
            TextOptions.SetTextFormattingMode(label, TextFormattingMode.Display);

            var row = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Stretch };
            row.Children.Add(figure);
            row.Children.Add(status);
            row.Children.Add(barPanel);
            row.Children.Add(dots);
            row.Children.Add(label);
            pill.Child = row;
            Content = pill;

            frame.Tick += delegate { Animate(); };
            hideTimer.Tick += delegate { hideTimer.Stop(); HideNow(); };
            SizeChanged += delegate { Place(); };

            SourceInitialized += delegate
            {
                // TOOLWINDOW | TRANSPARENT (click-through) | NOACTIVATE
                IntPtr h = new WindowInteropHelper(this).Handle;
                int ex = GetWindowLong(h, -20);
                SetWindowLong(h, -20, ex | 0x80 | 0x20 | 0x08000000);
            };
        }

        [DllImport("user32.dll")] static extern int GetWindowLong(IntPtr h, int i);
        [DllImport("user32.dll")] static extern int SetWindowLong(IntPtr h, int i, int v);

        public void ShowListening()
        {
            mode = Mode.Listening;
            since = DateTime.UtcNow;
            figureMove.BeginAnimation(TranslateTransform.XProperty, null);
            figureMove.BeginAnimation(TranslateTransform.YProperty, null);
            figureMove.X = figureMove.Y = 0;
            voice = 0;
            talkFrame = -1;
            for (int i = 0; i < Bars; i++) { targets[i] = 0; heights[i] = 3; }
            SetPose("talk-0");
            SetStatus(Red, false);
            barPanel.Visibility = Visibility.Visible;
            dots.Visibility = Visibility.Collapsed;
            label.Text = "Yap away  0:00";
            Present(0);
        }

        public void ShowWorking(string text)
        {
            bool wasWorking = mode == Mode.Working;
            mode = Mode.Working;
            figureMove.BeginAnimation(TranslateTransform.YProperty, null);
            SetPose(text.StartsWith("Tidying") ? "tidying" : "writing");
            SetStatus(Colors.Transparent, false);
            barPanel.Visibility = Visibility.Collapsed;
            dots.Visibility = Visibility.Visible;
            label.Text = text;
            if (!wasWorking) Present(0);
        }

        public void ShowMessage(string text, Color color, int ms)
        {
            mode = Mode.Message;
            barPanel.Visibility = Visibility.Collapsed;
            dots.Visibility = Visibility.Collapsed;
            label.Text = text;
            SetPose(color == Green ? "done" : color == Amber ? "oops" : "idle");
            SetStatus(color, color == Green);
            Present(ms);
            if (color == Green) Hop();
            else if (color == Amber) Wobble();
        }

        // Safe to call from the recorder thread.
        public void PushLevel(float level)
        {
            Dispatcher.BeginInvoke((Action)delegate
            {
                targets[barPos] = level;
                barPos = (barPos + 1) % Bars;
                voice = Math.Max(level, voice * 0.6);
            });
        }

        public void HideNow()
        {
            if (mode == Mode.Hidden) return;
            mode = Mode.Hidden;
            hideTimer.Stop();
            var fade = new DoubleAnimation(0, TimeSpan.FromMilliseconds(180));
            fade.Completed += delegate { if (mode == Mode.Hidden) { Hide(); frame.Stop(); } };
            pill.BeginAnimation(OpacityProperty, fade);
            K.To(pillScale, ScaleTransform.ScaleXProperty, 0.92, 180, K.Out);
            K.To(pillScale, ScaleTransform.ScaleYProperty, 0.92, 180, K.Out);
        }

        void SetPose(string name)
        {
            if (name == pose) return;
            bool talking = name.StartsWith("talk-") && pose != null && pose.StartsWith("talk-");
            pose = name;
            var img = K.Image(name + ".png");
            if (img != null) figure.Source = img;
            if (talking) return; // mouth frames swap instantly
            // A little pop whenever she changes pose.
            figureScale.ScaleX = figureScale.ScaleY = 0.82;
            var ease = new BackEase { Amplitude = 0.6, EasingMode = EasingMode.EaseOut };
            K.To(figureScale, ScaleTransform.ScaleXProperty, 1, 380, ease);
            K.To(figureScale, ScaleTransform.ScaleYProperty, 1, 380, ease);
        }

        void SetStatus(Color color, bool check)
        {
            status.Children.Clear();
            status.Visibility = color.A == 0 ? Visibility.Collapsed : Visibility.Visible;
            if (color.A == 0) return;
            if (check)
            {
                status.Children.Add(new Ellipse { Fill = new SolidColorBrush(color) });
                var c = K.Check(Color.FromRgb(16, 24, 20), 8);
                c.HorizontalAlignment = HorizontalAlignment.Center;
                status.Children.Add(c);
                var s = new ScaleTransform(0.3, 0.3);
                status.RenderTransform = s;
                status.RenderTransformOrigin = new Point(0.5, 0.5);
                var ease = new BackEase { Amplitude = 0.7, EasingMode = EasingMode.EaseOut };
                K.To(s, ScaleTransform.ScaleXProperty, 1, 380, ease);
                K.To(s, ScaleTransform.ScaleYProperty, 1, 380, ease);
            }
            else
            {
                status.RenderTransform = null;
                status.Children.Add(new Ellipse { Width = 10, Height = 10, Fill = new SolidColorBrush(color) });
            }
        }

        // Done: a quick happy jump.
        void Hop()
        {
            var a = new DoubleAnimationUsingKeyFrames();
            a.KeyFrames.Add(new EasingDoubleKeyFrame(0, KeyTime.FromTimeSpan(TimeSpan.Zero)));
            a.KeyFrames.Add(new EasingDoubleKeyFrame(-9, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(170)), new QuadraticEase { EasingMode = EasingMode.EaseOut }));
            a.KeyFrames.Add(new EasingDoubleKeyFrame(0, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(360)), new BounceEase { Bounces = 1, Bounciness = 3, EasingMode = EasingMode.EaseOut }));
            figureMove.BeginAnimation(TranslateTransform.YProperty, a);
        }

        // Error: a confused little side-to-side wobble.
        void Wobble()
        {
            var a = new DoubleAnimationUsingKeyFrames();
            double[] xs = { 0, -3, 3, -2, 2, 0 };
            for (int i = 0; i < xs.Length; i++)
                a.KeyFrames.Add(new LinearDoubleKeyFrame(xs[i], KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(i * 70))));
            figureMove.BeginAnimation(TranslateTransform.XProperty, a);
        }

        void Present(int autoHideMs)
        {
            hideTimer.Stop();
            if (autoHideMs > 0) { hideTimer.Interval = TimeSpan.FromMilliseconds(autoHideMs); hideTimer.Start(); }
            bool appearing = !IsVisible || pill.Opacity < 0.99;
            if (!IsVisible) { pill.Opacity = 0; Show(); }
            Place();
            if (appearing)
            {
                pillScale.ScaleX = pillScale.ScaleY = 0.85;
                pill.BeginAnimation(OpacityProperty, new DoubleAnimation(1, TimeSpan.FromMilliseconds(180)));
                var ease = new BackEase { Amplitude = 0.45, EasingMode = EasingMode.EaseOut };
                K.To(pillScale, ScaleTransform.ScaleXProperty, 1, 420, ease);
                K.To(pillScale, ScaleTransform.ScaleYProperty, 1, 420, ease);
            }
            frame.Start();
        }

        // Positions the bubble on the screen with the mouse, in WPF's device-independent units.
        void Place()
        {
            if (!IsVisible) return;
            var screen = System.Windows.Forms.Screen.FromPoint(System.Windows.Forms.Cursor.Position).WorkingArea;
            var source = PresentationSource.FromVisual(this);
            double sx = 1, sy = 1;
            if (source != null && source.CompositionTarget != null)
            {
                sx = source.CompositionTarget.TransformToDevice.M11;
                sy = source.CompositionTarget.TransformToDevice.M22;
            }
            double left = screen.Left / sx, top = screen.Top / sy, width = screen.Width / sx, height = screen.Height / sy;
            double w = ActualWidth, h = ActualHeight;
            double x = Position.EndsWith("left") ? left : Position.EndsWith("right") ? left + width - w : left + (width - w) / 2;
            double y = Position.StartsWith("bottom") ? top + height - h - 8 : top - 8;
            Left = x;
            Top = y;
        }

        void Animate()
        {
            tick += 1;
            if (mode == Mode.Listening)
            {
                var t = DateTime.UtcNow - since;
                string time = (int)t.TotalMinutes + ":" + t.Seconds.ToString("00");
                if (!label.Text.EndsWith(time)) label.Text = "Yap away  " + time;
                status.Opacity = 0.55 + 0.45 * (0.5 + 0.5 * Math.Sin(tick * 0.2));
                for (int i = 0; i < Bars; i++)
                {
                    double target = 3 + targets[(barPos + i) % Bars] * 19;
                    heights[i] += (target - heights[i]) * 0.35; // ease toward the new level
                    bars[i].Height = heights[i];
                }
                // Her mouth follows your voice: closed, half open or wide open.
                voice *= 0.92;
                int want = voice < 0.18 ? 0 : voice < 0.42 ? ((int)(tick / 3) % 2 == 0 ? 1 : 3) : 2;
                if (want != talkFrame && tick % 2 == 0)
                {
                    talkFrame = want;
                    SetPose("talk-" + want);
                }
            }
            else
            {
                status.Opacity = 1;
                if (mode == Mode.Working)
                {
                    // Gentle bob while she works.
                    figureMove.Y = -2.5 * Math.Abs(Math.Sin(tick * 0.16));
                    for (int i = 0; i < dots.Children.Count; i++)
                    {
                        double phase = tick * 0.22 - i * 0.8;
                        double lift = Math.Max(0, Math.Sin(phase));
                        var dot = (Ellipse)dots.Children[i];
                        ((TranslateTransform)dot.RenderTransform).Y = -lift * 4;
                        dot.Opacity = 0.45 + 0.55 * lift;
                    }
                }
            }
        }
    }
}
