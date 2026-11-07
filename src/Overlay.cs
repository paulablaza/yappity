using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace Saywrite
{
    // The small pill near the bottom of the screen: "Listening" with live level
    // bars, "Transcribing", then a short result message. It never takes focus and
    // clicks pass through it, so the app you are typing in stays active.
    internal sealed class Overlay : Form
    {
        enum Mode { Hidden, Listening, Working, Message }

        const int Bars = 16;
        static readonly Color Bg = Color.FromArgb(28, 28, 32);
        static readonly Color Red = Color.FromArgb(239, 68, 68);
        public static readonly Color Green = Color.FromArgb(52, 211, 153);
        public static readonly Color Amber = Color.FromArgb(251, 191, 36);
        public static readonly Color Muted = Color.FromArgb(161, 161, 170);

        Mode mode = Mode.Hidden;
        readonly float[] levels = new float[Bars];
        int levelPos;
        string text = "";
        Color dot = Red;
        int tick;
        readonly Timer anim = new Timer();
        readonly Timer hideTimer = new Timer();
        readonly float scale;

        public Overlay()
        {
            FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.Manual;
            BackColor = Bg;
            ForeColor = Color.FromArgb(244, 244, 245);
            DoubleBuffered = true;
            Opacity = 0.96;
            Font = new Font("Segoe UI", 9.75f, FontStyle.Regular);
            IntPtr force = Handle; // create the window now so other threads can BeginInvoke
            scale = DeviceDpi / 96f;

            anim.Interval = 40;
            anim.Tick += delegate { tick++; Invalidate(); };
            hideTimer.Tick += delegate { hideTimer.Stop(); HideNow(); };
        }

        protected override bool ShowWithoutActivation { get { return true; } }

        protected override CreateParams CreateParams
        {
            get
            {
                var cp = base.CreateParams;
                // TOPMOST | TOOLWINDOW | TRANSPARENT | NOACTIVATE
                cp.ExStyle |= 0x00000008 | 0x00000080 | 0x00000020 | 0x08000000;
                return cp;
            }
        }

        protected override void WndProc(ref Message m)
        {
            const int WM_NCHITTEST = 0x84, WM_MOUSEACTIVATE = 0x21;
            if (m.Msg == WM_NCHITTEST) { m.Result = (IntPtr)(-1); return; }      // click-through
            if (m.Msg == WM_MOUSEACTIVATE) { m.Result = (IntPtr)3; return; }      // never activate
            base.WndProc(ref m);
        }

        int S(float v) { return (int)Math.Round(v * scale); }

        public void ShowListening()
        {
            for (int i = 0; i < Bars; i++) levels[i] = 0;
            mode = Mode.Listening;
            text = "Listening";
            dot = Red;
            Present(S(30) + Bars * S(5) + S(10) + Measure(text) + S(20), 0);
        }

        public void ShowWorking(string message)
        {
            mode = Mode.Working;
            text = message;
            Present(S(52) + Measure(text) + S(20), 0);
        }

        public void ShowMessage(string message, Color color, int milliseconds)
        {
            mode = Mode.Message;
            text = message;
            dot = color;
            Present(S(32) + Measure(text) + S(20), milliseconds);
        }

        // Safe to call from the recorder thread.
        public void PushLevel(float level)
        {
            if (!IsHandleCreated) return;
            BeginInvoke((Action)delegate
            {
                levels[levelPos] = level;
                levelPos = (levelPos + 1) % Bars;
            });
        }

        public void HideNow()
        {
            mode = Mode.Hidden;
            anim.Stop();
            hideTimer.Stop();
            Hide();
        }

        int Measure(string s)
        {
            return Math.Min(S(520), TextRenderer.MeasureText(s, Font).Width);
        }

        void Present(int width, int autoHideMs)
        {
            int height = S(40);
            Rectangle area = Screen.FromPoint(Cursor.Position).WorkingArea;
            SetBounds(area.Left + (area.Width - width) / 2, area.Bottom - height - S(56), width, height);
            using (var path = Pill(new Rectangle(0, 0, width, height)))
            {
                var old = Region;
                Region = new Region(path);
                if (old != null) old.Dispose();
            }
            hideTimer.Stop();
            if (autoHideMs > 0) { hideTimer.Interval = autoHideMs; hideTimer.Start(); }
            if (!Visible) Show();
            anim.Start();
            Invalidate();
        }

        static GraphicsPath Pill(Rectangle r)
        {
            var p = new GraphicsPath();
            int d = r.Height;
            p.AddArc(r.Left, r.Top, d, d, 90, 180);
            p.AddArc(r.Right - d, r.Top, d, d, 270, 180);
            p.CloseFigure();
            return p;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            float cy = Height / 2f;
            int textLeft;

            if (mode == Mode.Listening)
            {
                float pulse = 0.55f + 0.45f * (float)(0.5 + 0.5 * Math.Sin(tick * 0.18));
                using (var b = new SolidBrush(Color.FromArgb((int)(255 * pulse), Red)))
                    g.FillEllipse(b, S(13), cy - S(5), S(10), S(10));

                float x = S(30);
                float barW = S(3), step = S(5), maxH = Height - S(16);
                using (var b = new SolidBrush(Color.FromArgb(230, 244, 244, 245)))
                {
                    for (int i = 0; i < Bars; i++)
                    {
                        float v = levels[(levelPos + i) % Bars];
                        float h = Math.Max(S(3), v * maxH);
                        g.FillRectangle(b, x + i * step, cy - h / 2, barW, h);
                    }
                }
                textLeft = S(30) + Bars * S(5) + S(10);
            }
            else if (mode == Mode.Working)
            {
                for (int i = 0; i < 3; i++)
                {
                    double phase = tick * 0.25 - i * 0.7;
                    float lift = (float)Math.Max(0, Math.Sin(phase)) * S(4);
                    int alpha = 120 + (int)(135 * Math.Max(0, Math.Sin(phase)));
                    using (var b = new SolidBrush(Color.FromArgb(alpha, Amber)))
                        g.FillEllipse(b, S(14) + i * S(10), cy - S(3) - lift, S(6), S(6));
                }
                textLeft = S(52);
            }
            else
            {
                using (var b = new SolidBrush(dot))
                    g.FillEllipse(b, S(14), cy - S(4), S(8), S(8));
                textLeft = S(32);
            }

            var rect = new Rectangle(textLeft, 0, Width - textLeft - S(14), Height);
            TextRenderer.DrawText(g, text, Font, rect, ForeColor,
                TextFormatFlags.VerticalCenter | TextFormatFlags.Left | TextFormatFlags.NoPrefix |
                TextFormatFlags.EndEllipsis | TextFormatFlags.SingleLine);
        }
    }
}
