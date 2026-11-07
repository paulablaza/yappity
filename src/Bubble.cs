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
    // The floating pill with Chatter-Chan's face: "Yap away" with live voice bars,
    // then "Writing it down", then a short result. It never takes focus and clicks
    // pass straight through it.
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
        readonly Rectangle[] bars = new Rectangle[Bars];
        readonly double[] targets = new double[Bars];
        readonly double[] heights = new double[Bars];
        int barPos;

        readonly Border pill = new Border();
        readonly ScaleTransform pillScale = new ScaleTransform(1, 1);
        readonly Ellipse face = new Ellipse { Width = 32, Height = 32 };
        readonly ScaleTransform faceScale = new ScaleTransform(1, 1);
        readonly Grid badge = new Grid { Width = 14, Height = 14, HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Bottom, Margin = new Thickness(0, 0, -2, -2) };
        readonly StackPanel barPanel = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 12, 0), Height = 22, VerticalAlignment = VerticalAlignment.Center };
        readonly StackPanel dots = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 10, 0) };
        readonly TextBlock label = new TextBlock();
        readonly DispatcherTimer frame = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(33) };
        readonly DispatcherTimer hideTimer = new DispatcherTimer();
        Mode mode = Mode.Hidden;
        string currentFace;
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
            pill.Padding = new Thickness(9, 0, 20, 0);
            pill.Background = new SolidColorBrush(Color.FromArgb(242, 22, 22, 26));
            pill.BorderBrush = new SolidColorBrush(Color.FromArgb(38, 255, 255, 255));
            pill.BorderThickness = new Thickness(1);
            pill.Effect = K.Shadow(26, 0.45);
            pill.Margin = new Thickness(26, 14, 26, 32); // room for the shadow
            pill.RenderTransform = pillScale;
            pill.RenderTransformOrigin = new Point(0.5, 0.5);

            var avatar = new Grid { Width = 32, Height = 32, Margin = new Thickness(0, 0, 11, 0), VerticalAlignment = VerticalAlignment.Center };
            face.Fill = new SolidColorBrush(Color.FromRgb(60, 50, 120));
            face.Stroke = new SolidColorBrush(Color.FromArgb(60, 255, 255, 255));
            face.StrokeThickness = 1;
            avatar.Children.Add(face);
            avatar.Children.Add(badge);
            avatar.RenderTransform = faceScale;
            avatar.RenderTransformOrigin = new Point(0.5, 0.5);

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

            var row = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
            row.Children.Add(avatar);
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
            for (int i = 0; i < Bars; i++) { targets[i] = 0; heights[i] = 3; }
            SetFace("yapping");
            SetBadge(Red, false);
            barPanel.Visibility = Visibility.Visible;
            dots.Visibility = Visibility.Collapsed;
            label.Text = "Yap away  0:00";
            Present(0);
        }

        public void ShowWorking(string text)
        {
            bool wasWorking = mode == Mode.Working;
            mode = Mode.Working;
            SetFace(text.StartsWith("Tidying") ? "tidying" : "writing");
            SetBadge(Colors.Transparent, false);
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
            SetFace(color == Green ? "done" : color == Amber ? "oops" : "idle");
            SetBadge(color == Green ? Green : Colors.Transparent, color == Green);
            Present(ms);
        }

        // Safe to call from the recorder thread.
        public void PushLevel(float level)
        {
            Dispatcher.BeginInvoke((Action)delegate
            {
                targets[barPos] = level;
                barPos = (barPos + 1) % Bars;
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

        void SetFace(string name)
        {
            if (name == currentFace) return;
            currentFace = name;
            var img = K.Image("face-" + name + ".png");
            if (img != null) face.Fill = new ImageBrush(img) { Stretch = Stretch.UniformToFill };
            // A small bounce whenever her face changes.
            faceScale.ScaleX = faceScale.ScaleY = 0.75;
            var ease = new BackEase { Amplitude = 0.6, EasingMode = EasingMode.EaseOut };
            K.To(faceScale, ScaleTransform.ScaleXProperty, 1, 360, ease);
            K.To(faceScale, ScaleTransform.ScaleYProperty, 1, 360, ease);
        }

        void SetBadge(Color color, bool check)
        {
            badge.Children.Clear();
            if (color.A == 0) return;
            badge.Children.Add(new Ellipse { Fill = new SolidColorBrush(color), Stroke = new SolidColorBrush(Color.FromRgb(22, 22, 26)), StrokeThickness = 2 });
            if (check)
            {
                var c = K.Check(Color.FromRgb(16, 24, 20), 7);
                c.HorizontalAlignment = HorizontalAlignment.Center;
                badge.Children.Add(c);
            }
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
            double y = Position.StartsWith("bottom") ? top + height - h - 8 : top;
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
                if (badge.Children.Count > 0) badge.Opacity = 0.55 + 0.45 * (0.5 + 0.5 * Math.Sin(tick * 0.2));
                for (int i = 0; i < Bars; i++)
                {
                    double target = 3 + targets[(barPos + i) % Bars] * 19;
                    heights[i] += (target - heights[i]) * 0.35; // ease toward the new level
                    bars[i].Height = heights[i];
                }
            }
            else
            {
                badge.Opacity = 1;
                if (mode == Mode.Working)
                {
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
