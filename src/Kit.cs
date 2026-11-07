using System;
using System.Collections.Generic;
using Stream = System.IO.Stream;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace Yappity
{
    // Colors, fonts, motion and the small set of controls the window is built from.
    internal static class K
    {
        public static bool Dark;
        public static Color Bg, Surface, Surface2, Line, Text, Muted, Accent, AccentHover, AccentSoft, Success, Warn, Danger, Track;
        public static readonly FontFamily Font = new FontFamily("Segoe UI");

        public static readonly IEasingFunction Out = new CubicEase { EasingMode = EasingMode.EaseOut };
        public static readonly IEasingFunction Spring = new BackEase { Amplitude = 0.35, EasingMode = EasingMode.EaseOut };

        public static void Load()
        {
            Dark = Theme.Dark;
            if (Dark)
            {
                Bg = Hex(0x111114); Surface = Hex(0x1B1B20); Surface2 = Hex(0x25252C); Line = Hex(0x2D2D35);
                Text = Hex(0xF5F5F7); Muted = Hex(0x9E9EA8); Accent = Hex(0x8B7FFF); AccentHover = Hex(0x9D93FF);
                AccentSoft = Hex(0x262247); Success = Hex(0x34D399); Warn = Hex(0xFBBF24); Danger = Hex(0xF87171); Track = Hex(0x3A3A44);
            }
            else
            {
                Bg = Hex(0xF5F5F7); Surface = Hex(0xFFFFFF); Surface2 = Hex(0xF0F0F4); Line = Hex(0xE3E3E8);
                Text = Hex(0x1D1D1F); Muted = Hex(0x6E6E76); Accent = Hex(0x5B4BEA); AccentHover = Hex(0x4C3CD6);
                AccentSoft = Hex(0xEEEBFF); Success = Hex(0x16A34A); Warn = Hex(0xB45309); Danger = Hex(0xDC2626); Track = Hex(0xD1D1D8);
            }
        }

        public static Color Hex(int rgb) { return Color.FromRgb((byte)(rgb >> 16), (byte)(rgb >> 8), (byte)rgb); }
        public static Color Alpha(Color c, byte a) { return Color.FromArgb(a, c.R, c.G, c.B); }
        public static SolidColorBrush B(Color c) { return new SolidColorBrush(c); }

        public static TextBlock T(string text, double size, Color color, FontWeight weight)
        {
            var t = new TextBlock();
            t.Text = text;
            t.FontFamily = Font;
            t.FontSize = size;
            t.FontWeight = weight;
            t.Foreground = B(color);
            TextOptions.SetTextFormattingMode(t, TextFormattingMode.Display);
            return t;
        }

        public static TextBlock Wrap(string text, double size, Color color)
        {
            var t = T(text, size, color, FontWeights.Normal);
            t.TextWrapping = TextWrapping.Wrap;
            t.LineHeight = size * 1.42;
            return t;
        }

        public static Effect Shadow(double blur, double opacity)
        {
            return new DropShadowEffect { Color = Colors.Black, BlurRadius = blur, ShadowDepth = blur / 6, Direction = 270, Opacity = opacity };
        }

        public static void To(IAnimatable target, DependencyProperty prop, double to, int ms, IEasingFunction ease)
        {
            var a = new DoubleAnimation(to, TimeSpan.FromMilliseconds(ms));
            a.EasingFunction = ease;
            target.BeginAnimation(prop, a);
        }

        public static void ColorTo(SolidColorBrush brush, Color to, int ms)
        {
            var a = new ColorAnimation(to, TimeSpan.FromMilliseconds(ms));
            a.EasingFunction = Out;
            brush.BeginAnimation(SolidColorBrush.ColorProperty, a);
        }

        // Fades an element in while it slides up a few pixels.
        public static void Rise(FrameworkElement e, int delayMs)
        {
            var move = new TranslateTransform(0, 10);
            e.RenderTransform = move;
            e.Opacity = 0;
            var fade = new DoubleAnimation(1, TimeSpan.FromMilliseconds(260)) { BeginTime = TimeSpan.FromMilliseconds(delayMs), EasingFunction = Out };
            var slide = new DoubleAnimation(0, TimeSpan.FromMilliseconds(320)) { BeginTime = TimeSpan.FromMilliseconds(delayMs), EasingFunction = Out };
            e.BeginAnimation(UIElement.OpacityProperty, fade);
            move.BeginAnimation(TranslateTransform.YProperty, slide);
        }

        public static void Hover(Border b, SolidColorBrush brush, Color normal, Color hover)
        {
            b.MouseEnter += delegate { ColorTo(brush, hover, 140); };
            b.MouseLeave += delegate { ColorTo(brush, normal, 200); };
        }

        static readonly Dictionary<string, ImageSource> images = new Dictionary<string, ImageSource>();

        // Images built into the .exe (see build.ps1). Returns null if missing.
        public static ImageSource Image(string name)
        {
            ImageSource cached;
            if (images.TryGetValue(name, out cached)) return cached;
            ImageSource result = null;
            try
            {
                Stream s = Assembly.GetExecutingAssembly().GetManifestResourceStream(name);
                if (s != null)
                {
                    var bmp = new BitmapImage();
                    bmp.BeginInit();
                    bmp.StreamSource = s;
                    bmp.CacheOption = BitmapCacheOption.OnLoad;
                    bmp.EndInit();
                    bmp.Freeze();
                    result = bmp;
                }
            }
            catch (Exception) { }
            images[name] = result;
            return result;
        }

        public static System.Windows.Shapes.Path Chevron(Color c)
        {
            var p = new System.Windows.Shapes.Path();
            p.Data = Geometry.Parse("M0,0 L4.5,4.5 L9,0");
            p.Stroke = B(c);
            p.StrokeThickness = 1.6;
            p.StrokeStartLineCap = PenLineCap.Round;
            p.StrokeEndLineCap = PenLineCap.Round;
            p.StrokeLineJoin = PenLineJoin.Round;
            p.VerticalAlignment = VerticalAlignment.Center;
            return p;
        }

        public static System.Windows.Shapes.Path Check(Color c, double size)
        {
            var p = new System.Windows.Shapes.Path();
            p.Data = Geometry.Parse("M0,5 L4,9 L12,0");
            p.Stroke = B(c);
            p.StrokeThickness = 2;
            p.StrokeStartLineCap = PenLineCap.Round;
            p.StrokeEndLineCap = PenLineCap.Round;
            p.StrokeLineJoin = PenLineJoin.Round;
            p.Width = size;
            p.Height = size;
            p.Stretch = Stretch.Uniform;
            p.VerticalAlignment = VerticalAlignment.Center;
            return p;
        }
    }

    // A rounded button that presses down slightly when clicked.
    internal class PillButton : Border
    {
        public event Action Click;
        protected readonly TextBlock Label;
        protected readonly StackPanel Row = new StackPanel { Orientation = Orientation.Horizontal };
        readonly SolidColorBrush bg = new SolidColorBrush();
        readonly ScaleTransform scale = new ScaleTransform(1, 1);
        bool primary;

        public PillButton(string text, bool primary)
        {
            CornerRadius = new CornerRadius(10);
            Padding = new Thickness(14, 7, 14, 8);
            Cursor = Cursors.Hand;
            Focusable = true;
            FocusVisualStyle = null;
            Background = bg;
            RenderTransform = scale;
            RenderTransformOrigin = new Point(0.5, 0.5);
            VerticalAlignment = VerticalAlignment.Center;
            Label = K.T(text, 13, K.Text, FontWeights.SemiBold);
            Row.Children.Add(Label);
            Child = Row;
            Primary = primary;

            MouseEnter += delegate { K.ColorTo(bg, this.primary ? K.AccentHover : K.Surface2, 140); };
            MouseLeave += delegate { K.ColorTo(bg, Rest, 200); K.To(scale, ScaleTransform.ScaleXProperty, 1, 160, K.Out); K.To(scale, ScaleTransform.ScaleYProperty, 1, 160, K.Out); };
            MouseLeftButtonDown += delegate(object s, MouseButtonEventArgs e)
            {
                K.To(scale, ScaleTransform.ScaleXProperty, 0.96, 90, K.Out);
                K.To(scale, ScaleTransform.ScaleYProperty, 0.96, 90, K.Out);
                CaptureMouse();
                e.Handled = true;
            };
            MouseLeftButtonUp += delegate(object s, MouseButtonEventArgs e)
            {
                bool inside = IsMouseOver;
                ReleaseMouseCapture();
                K.To(scale, ScaleTransform.ScaleXProperty, 1, 220, K.Spring);
                K.To(scale, ScaleTransform.ScaleYProperty, 1, 220, K.Spring);
                if (inside && IsEnabled) Fire();
                e.Handled = true;
            };
            KeyDown += delegate(object s, KeyEventArgs e)
            {
                if (e.Key == Key.Enter || e.Key == Key.Space) { Fire(); e.Handled = true; }
            };
            IsEnabledChanged += delegate { Opacity = IsEnabled ? 1 : 0.5; };
        }

        Color Rest { get { return primary ? K.Accent : K.Surface; } }

        public bool Primary
        {
            get { return primary; }
            set
            {
                primary = value;
                bg.Color = Rest;
                BorderBrush = K.B(primary ? Colors.Transparent : K.Line);
                BorderThickness = new Thickness(primary ? 0 : 1);
                Label.Foreground = K.B(primary ? Colors.White : K.Text);
            }
        }

        public string Text
        {
            get { return Label.Text; }
            set { Label.Text = value; }
        }

        protected void Fire()
        {
            if (Click != null) Click();
        }
    }

    // iOS-style switch with a sliding knob.
    internal sealed class Toggle : Border
    {
        public event Action Changed;
        readonly SolidColorBrush track = new SolidColorBrush();
        readonly TranslateTransform knobMove = new TranslateTransform();
        bool on;

        public Toggle()
        {
            Width = 44;
            Height = 26;
            CornerRadius = new CornerRadius(13);
            Background = track;
            Cursor = Cursors.Hand;
            VerticalAlignment = VerticalAlignment.Center;
            var knob = new Ellipse { Width = 20, Height = 20, Fill = Brushes.White, HorizontalAlignment = HorizontalAlignment.Left };
            knob.Margin = new Thickness(3, 0, 0, 0);
            knob.RenderTransform = knobMove;
            knob.Effect = K.Shadow(6, 0.25);
            Child = knob;
            Set(false, false);
            MouseLeftButtonUp += delegate { Set(!on, true); if (Changed != null) Changed(); };
        }

        public bool IsOn
        {
            get { return on; }
            set { Set(value, false); }
        }

        void Set(bool value, bool animate)
        {
            on = value;
            int ms = animate ? 260 : 0;
            K.ColorTo(track, on ? K.Accent : K.Track, ms);
            K.To(knobMove, TranslateTransform.XProperty, on ? 18 : 0, ms, K.Spring);
        }
    }

    // A button showing the current choice; clicking it opens a small menu.
    internal sealed class Picker : PillButton
    {
        public event Action Changed;
        Choice[] choices = new Choice[0];
        string value = "";
        readonly Popup popup = new Popup();

        public Picker() : base("", false)
        {
            var chev = K.Chevron(K.Muted);
            chev.Margin = new Thickness(10, 2, 0, 0);
            Row.Children.Add(chev);
            popup.PlacementTarget = this;
            popup.Placement = PlacementMode.Bottom;
            popup.StaysOpen = false;
            popup.AllowsTransparency = true;
            popup.PopupAnimation = PopupAnimation.Fade;
            Click += Open;
        }

        public void SetChoices(Choice[] list)
        {
            choices = list;
            Value = value;
        }

        public string Value
        {
            get { return value; }
            set
            {
                this.value = value ?? "";
                if (choices.Length > 0) Text = Groq.Find(choices, this.value).Name;
            }
        }

        public Choice Selected { get { return choices.Length > 0 ? Groq.Find(choices, value) : null; } }

        void Open()
        {
            var list = new StackPanel();
            foreach (var c in choices)
            {
                var choice = c;
                var bg = new SolidColorBrush(Colors.Transparent);
                var item = new Border { CornerRadius = new CornerRadius(7), Padding = new Thickness(10, 7, 14, 7), Background = bg, Cursor = Cursors.Hand };
                var row = new StackPanel { Orientation = Orientation.Horizontal };
                var check = K.Check(K.Accent, 11);
                check.Margin = new Thickness(0, 0, 10, 0);
                check.Opacity = choice.Id == value ? 1 : 0;
                row.Children.Add(check);
                row.Children.Add(K.T(choice.Name, 13, K.Text, FontWeights.Normal));
                item.Child = row;
                K.Hover(item, bg, Colors.Transparent, K.Surface2);
                item.MouseLeftButtonUp += delegate
                {
                    popup.IsOpen = false;
                    Value = choice.Id;
                    if (Changed != null) Changed();
                };
                list.Children.Add(item);
            }
            var card = new Border
            {
                Background = K.B(K.Surface),
                BorderBrush = K.B(K.Line),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(12),
                Padding = new Thickness(5),
                Margin = new Thickness(10, 6, 10, 16),
                Effect = K.Shadow(18, K.Dark ? 0.5 : 0.16),
                Child = list,
                MinWidth = ActualWidth,
            };
            popup.Child = card;
            popup.HorizontalOffset = -10;
            popup.IsOpen = true;
            K.Rise(card, 0);
        }
    }

    // Text input with a soft border that turns purple when focused.
    internal sealed class Field : Border
    {
        public readonly TextBox Box = new TextBox();
        readonly PasswordBox secret;
        readonly TextBlock placeholder;
        readonly SolidColorBrush border = new SolidColorBrush();
        bool showing;

        public Field(string hint, bool multiline, bool masked)
        {
            CornerRadius = new CornerRadius(10);
            Background = K.B(K.Surface);
            BorderBrush = border;
            BorderThickness = new Thickness(1);
            border.Color = K.Line;
            Padding = new Thickness(10, multiline ? 8 : 6, 10, multiline ? 8 : 6);
            Cursor = Cursors.IBeam;

            var grid = new Grid();
            StyleBox(Box);
            Box.AcceptsReturn = false;
            if (multiline)
            {
                Box.TextWrapping = TextWrapping.Wrap;
                Box.VerticalContentAlignment = VerticalAlignment.Top;
                Height = 92;
            }
            placeholder = K.T(hint, 13.5, K.Muted, FontWeights.Normal);
            placeholder.Margin = new Thickness(3, multiline ? 1 : 2, 0, 0);
            placeholder.IsHitTestVisible = false;
            placeholder.VerticalAlignment = multiline ? VerticalAlignment.Top : VerticalAlignment.Center;
            grid.Children.Add(placeholder);
            grid.Children.Add(Box);

            if (masked)
            {
                secret = new PasswordBox();
                secret.Background = Brushes.Transparent;
                secret.BorderThickness = new Thickness(0);
                secret.Foreground = K.B(K.Text);
                secret.CaretBrush = K.B(K.Text);
                secret.FontFamily = K.Font;
                secret.FontSize = 13.5;
                secret.VerticalContentAlignment = VerticalAlignment.Center;
                secret.FocusVisualStyle = null;
                secret.PasswordChanged += delegate { if (!showing) Box.Text = secret.Password; UpdateHint(); };
                secret.GotKeyboardFocus += delegate { Focus(true); };
                secret.LostKeyboardFocus += delegate { Focus(false); };
                grid.Children.Add(secret);
                Box.Visibility = Visibility.Collapsed;
            }
            Box.TextChanged += delegate
            {
                if (secret != null && showing && secret.Password != Box.Text) secret.Password = Box.Text;
                UpdateHint();
            };
            Box.GotKeyboardFocus += delegate { Focus(true); };
            Box.LostKeyboardFocus += delegate { Focus(false); };
            Child = grid;
            MouseLeftButtonDown += delegate { if (secret != null && !showing) secret.Focus(); else Box.Focus(); };
        }

        static void StyleBox(TextBox t)
        {
            t.Background = Brushes.Transparent;
            t.BorderThickness = new Thickness(0);
            t.Foreground = K.B(K.Text);
            t.CaretBrush = K.B(K.Text);
            t.SelectionBrush = K.B(K.Accent);
            t.FontFamily = K.Font;
            t.FontSize = 13.5;
            t.VerticalContentAlignment = VerticalAlignment.Center;
            t.FocusVisualStyle = null;
        }

        void Focus(bool focused)
        {
            K.ColorTo(border, focused ? K.Accent : K.Line, 160);
        }

        void UpdateHint()
        {
            placeholder.Visibility = Text.Length == 0 ? Visibility.Visible : Visibility.Collapsed;
        }

        public string Text
        {
            get { return Box.Text; }
            set
            {
                Box.Text = value ?? "";
                if (secret != null) secret.Password = value ?? "";
                UpdateHint();
            }
        }

        // For the API key: switch between dots and plain text.
        public bool Reveal
        {
            get { return showing; }
            set
            {
                if (secret == null) return;
                showing = value;
                Box.Visibility = showing ? Visibility.Visible : Visibility.Collapsed;
                secret.Visibility = showing ? Visibility.Collapsed : Visibility.Visible;
            }
        }

        public event TextChangedEventHandler TextChanged
        {
            add { Box.TextChanged += value; }
            remove { Box.TextChanged -= value; }
        }
    }

    // A rounded group of settings rows, like iOS Settings.
    internal sealed class Group : Border
    {
        readonly StackPanel rows = new StackPanel();

        public Group()
        {
            Background = K.B(K.Surface);
            CornerRadius = new CornerRadius(14);
            BorderBrush = K.B(K.Dark ? K.Line : K.Alpha(K.Line, 160));
            BorderThickness = new Thickness(1);
            if (!K.Dark) Effect = K.Shadow(14, 0.05);
            Child = rows;
        }

        public void Add(FrameworkElement row)
        {
            if (rows.Children.Count > 0)
                rows.Children.Add(new Rectangle { Height = 1, Fill = K.B(K.Line), Margin = new Thickness(16, 0, 0, 0) });
            rows.Children.Add(row);
        }

        // Title and description on the left, a control on the right.
        public static Grid Row(string title, TextBlock description, FrameworkElement control)
        {
            var g = new Grid { Margin = new Thickness(16, 12, 14, 12) };
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            var left = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            left.Children.Add(K.T(title, 14, K.Text, FontWeights.Medium));
            if (description != null)
            {
                description.Margin = new Thickness(0, 3, 0, 0);
                left.Children.Add(description);
            }
            g.Children.Add(left);
            if (control != null)
            {
                control.Margin = new Thickness(16, 0, 0, 0);
                control.VerticalAlignment = VerticalAlignment.Center;
                Grid.SetColumn(control, 1);
                g.Children.Add(control);
            }
            return g;
        }
    }

    // Two-option switch with a sliding highlight, for History / Settings.
    internal sealed class Segmented : Border
    {
        public event Action<int> Selected;
        readonly TranslateTransform slide = new TranslateTransform();
        readonly TextBlock[] labels;
        const double ItemWidth = 92;
        int index;

        public Segmented(params string[] items)
        {
            Background = K.B(K.Dark ? K.Hex(0x1F1F25) : K.Hex(0xE6E6EB));
            CornerRadius = new CornerRadius(10);
            Padding = new Thickness(3);
            var grid = new Grid { Width = ItemWidth * items.Length, Height = 30 };
            var pill = new Border
            {
                Width = ItemWidth,
                HorizontalAlignment = HorizontalAlignment.Left,
                CornerRadius = new CornerRadius(8),
                Background = K.B(K.Dark ? K.Hex(0x34343D) : Colors.White),
                Effect = K.Shadow(8, K.Dark ? 0.4 : 0.12),
                RenderTransform = slide,
            };
            grid.Children.Add(pill);
            labels = new TextBlock[items.Length];
            for (int i = 0; i < items.Length; i++)
            {
                int n = i;
                var hit = new Border { Background = Brushes.Transparent, Width = ItemWidth, HorizontalAlignment = HorizontalAlignment.Left, Margin = new Thickness(ItemWidth * i, 0, 0, 0), Cursor = Cursors.Hand };
                labels[i] = K.T(items[i], 13, K.Muted, FontWeights.SemiBold);
                labels[i].HorizontalAlignment = HorizontalAlignment.Center;
                labels[i].VerticalAlignment = VerticalAlignment.Center;
                hit.Child = labels[i];
                hit.MouseLeftButtonUp += delegate { Select(n, true); if (Selected != null) Selected(n); };
                grid.Children.Add(hit);
            }
            Child = grid;
            Select(0, false);
        }

        public int Index { get { return index; } }

        public void Select(int i, bool animate)
        {
            index = i;
            K.To(slide, TranslateTransform.XProperty, ItemWidth * i, animate ? 300 : 0, K.Spring);
            for (int n = 0; n < labels.Length; n++)
                K.ColorTo((SolidColorBrush)labels[n].Foreground, n == i ? K.Text : K.Muted, animate ? 200 : 0);
        }
    }

    // Shortcut shown as little keyboard keys, like Ctrl + Shift + K.
    internal sealed class KeyCaps : StackPanel
    {
        public KeyCaps()
        {
            Orientation = Orientation.Horizontal;
            VerticalAlignment = VerticalAlignment.Center;
        }

        public void Show(string shortcut)
        {
            Children.Clear();
            foreach (string part in shortcut.Split(new[] { " + " }, StringSplitOptions.RemoveEmptyEntries))
            {
                var cap = new Border
                {
                    Background = K.B(K.Surface2),
                    BorderBrush = K.B(K.Line),
                    BorderThickness = new Thickness(1, 1, 1, 2.5),
                    CornerRadius = new CornerRadius(7),
                    Padding = new Thickness(9, 3, 9, 4),
                    Margin = new Thickness(4, 0, 0, 0),
                    MinWidth = 30,
                };
                var t = K.T(part, 12.5, K.Text, FontWeights.SemiBold);
                t.HorizontalAlignment = HorizontalAlignment.Center;
                cap.Child = t;
                Children.Add(cap);
            }
        }
    }
}
