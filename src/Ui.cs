using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using Microsoft.Win32;

namespace Yappity
{
    // Colors, sizes and fonts. Follows the Windows light/dark app setting.
    internal static class Theme
    {
        public static bool Dark;
        public static Color Bg, Surface, SurfaceHover, Border, Text, Muted, Accent, AccentHover, AccentSoft, Danger, Warn, Success, Track;
        public static float Scale = 1f;
        static string fontName = "Segoe UI";

        static Theme() { Refresh(null); }

        // mode is "light", "dark", or anything else to follow the Windows setting.
        public static void Refresh(string mode)
        {
            Dark = false;
            if (mode == "dark") Dark = true;
            else if (mode != "light")
            {
                try
                {
                    using (var k = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize"))
                    {
                        object v = k != null ? k.GetValue("AppsUseLightTheme") : null;
                        Dark = v is int && (int)v == 0;
                    }
                }
                catch (Exception) { }
            }

            using (var g = Graphics.FromHwnd(IntPtr.Zero)) Scale = g.DpiX / 96f;
            try
            {
                if (FontFamily.Families.Any(f => f.Name == "Segoe UI Variable Text")) fontName = "Segoe UI Variable Text";
            }
            catch (Exception) { }

            if (Dark)
            {
                Bg = Hex(0x111113); Surface = Hex(0x1A1A1E); SurfaceHover = Hex(0x222227); Border = Hex(0x2E2E35);
                Text = Hex(0xF4F4F5); Muted = Hex(0x9C9CA6); Accent = Hex(0x7C7FF5); AccentHover = Hex(0x9093F7);
                AccentSoft = Hex(0x23234A); Danger = Hex(0xF87171); Warn = Hex(0xFBBF24); Success = Hex(0x34D399); Track = Hex(0x3A3A42);
            }
            else
            {
                Bg = Hex(0xF7F7F8); Surface = Hex(0xFFFFFF); SurfaceHover = Hex(0xF3F3F6); Border = Hex(0xE4E4E9);
                Text = Hex(0x18181B); Muted = Hex(0x6E6E78); Accent = Hex(0x4F46E5); AccentHover = Hex(0x4338CA);
                AccentSoft = Hex(0xEEF0FF); Danger = Hex(0xDC2626); Warn = Hex(0xB45309); Success = Hex(0x15803D); Track = Hex(0xD4D4DB);
            }
        }

        static Color Hex(int rgb) { return Color.FromArgb(255, (rgb >> 16) & 255, (rgb >> 8) & 255, rgb & 255); }

        public static int S(float v) { return (int)Math.Round(v * Scale); }

        public static Font F(float points) { return new Font(fontName, points, FontStyle.Regular); }
        public static Font Bold(float points)
        {
            try
            {
                var fam = new FontFamily(fontName == "Segoe UI" ? "Segoe UI Semibold" : "Segoe UI Variable Display Semib");
                return new Font(fam, points, FontStyle.Regular);
            }
            catch (Exception) { return new Font(fontName, points, FontStyle.Bold); }
        }

        public static GraphicsPath Round(RectangleF r, float radius)
        {
            float d = Math.Min(radius * 2, Math.Min(r.Width, r.Height));
            var p = new GraphicsPath();
            if (d <= 0) { p.AddRectangle(r); return p; }
            p.AddArc(r.Left, r.Top, d, d, 180, 90);
            p.AddArc(r.Right - d, r.Top, d, d, 270, 90);
            p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            p.AddArc(r.Left, r.Bottom - d, d, d, 90, 90);
            p.CloseFigure();
            return p;
        }

        public static void FillRound(Graphics g, Color fill, Color border, RectangleF r, float radius)
        {
            var rr = new RectangleF(r.X + 0.5f, r.Y + 0.5f, r.Width - 1, r.Height - 1);
            using (var path = Round(rr, radius))
            {
                using (var b = new SolidBrush(fill)) g.FillPath(b, path);
                if (border != Color.Empty)
                    using (var p = new Pen(border, Math.Max(1f, Scale))) g.DrawPath(p, path);
            }
        }

        [DllImport("uxtheme.dll", CharSet = CharSet.Unicode)]
        static extern int SetWindowTheme(IntPtr hwnd, string appName, string idList);

        // Dark scrollbars on panels that scroll.
        public static void StyleScrollbars(Control c)
        {
            if (!Dark) return;
            try { SetWindowTheme(c.Handle, "DarkMode_Explorer", null); } catch (Exception) { }
        }

        [DllImport("dwmapi.dll")]
        static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int value, int size);

        public static void StyleTitleBar(Form f)
        {
            try
            {
                int on = Dark ? 1 : 0;
                DwmSetWindowAttribute(f.Handle, 20, ref on, 4);        // dark title bar
                int round = 2;
                DwmSetWindowAttribute(f.Handle, 33, ref round, 4);     // rounded corners (Windows 11)
            }
            catch (Exception) { }
        }

        // Context menus (tray, dropdowns) in the same colors.
        sealed class MenuColors : ProfessionalColorTable
        {
            public override Color ToolStripDropDownBackground { get { return Surface; } }
            public override Color ImageMarginGradientBegin { get { return Surface; } }
            public override Color ImageMarginGradientMiddle { get { return Surface; } }
            public override Color ImageMarginGradientEnd { get { return Surface; } }
            public override Color MenuBorder { get { return Border; } }
            public override Color MenuItemBorder { get { return SurfaceHover; } }
            public override Color MenuItemSelected { get { return SurfaceHover; } }
            public override Color MenuItemSelectedGradientBegin { get { return SurfaceHover; } }
            public override Color MenuItemSelectedGradientEnd { get { return SurfaceHover; } }
            public override Color MenuItemPressedGradientBegin { get { return SurfaceHover; } }
            public override Color MenuItemPressedGradientEnd { get { return SurfaceHover; } }
            public override Color SeparatorDark { get { return Border; } }
            public override Color SeparatorLight { get { return Surface; } }
        }

        public static ContextMenuStrip Menu()
        {
            var m = new ContextMenuStrip();
            var r = new ToolStripProfessionalRenderer(new MenuColors());
            r.RoundedEdges = false;
            m.Renderer = r;
            m.BackColor = Surface;
            m.ForeColor = Text;
            m.Font = F(9.5f);
            m.ShowImageMargin = false;
            return m;
        }

        public static ToolStripMenuItem Item(string text, EventHandler click)
        {
            var i = new ToolStripMenuItem(text, null, click);
            i.ForeColor = Text;
            i.Padding = new Padding(S(4), S(3), S(14), S(3));
            return i;
        }
    }

    // Base for owner-drawn controls: double buffered, no flicker.
    internal abstract class Drawn : Control
    {
        protected bool Hover;
        protected Drawn()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.ResizeRedraw | ControlStyles.SupportsTransparentBackColor, true);
        }
        protected override void OnMouseEnter(EventArgs e) { Hover = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { Hover = false; Invalidate(); base.OnMouseLeave(e); }
        protected override void OnGotFocus(EventArgs e) { Invalidate(); base.OnGotFocus(e); }
        protected override void OnLostFocus(EventArgs e) { Invalidate(); base.OnLostFocus(e); }
        protected override void OnEnabledChanged(EventArgs e) { Invalidate(); base.OnEnabledChanged(e); }
    }

    internal sealed class FlatButton : Drawn
    {
        public bool Primary;
        public bool Chevron;

        public FlatButton(string text, bool primary)
        {
            Text = text;
            Primary = primary;
            Font = Theme.F(9.5f);
            Cursor = Cursors.Hand;
            TabStop = true;
            Fit();
        }

        public int PreferredWidth()
        {
            return TextRenderer.MeasureText(Text, Font).Width + Theme.S(Chevron ? 44 : 28);
        }

        public void Fit()
        {
            Size = new Size(PreferredWidth(), Theme.S(34));
        }

        protected override void OnTextChanged(EventArgs e) { base.OnTextChanged(e); Invalidate(); }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Space || e.KeyCode == Keys.Enter) OnClick(EventArgs.Empty);
            base.OnKeyDown(e);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(Parent != null ? Parent.BackColor : Theme.Bg);
            var r = new RectangleF(0, 0, Width, Height);
            Color fill, border, fore;
            if (Primary)
            {
                fill = Hover ? Theme.AccentHover : Theme.Accent; border = Color.Empty; fore = Color.White;
            }
            else
            {
                fill = Hover ? Theme.SurfaceHover : Theme.Surface; border = Focused ? Theme.Accent : Theme.Border; fore = Theme.Text;
            }
            if (!Enabled) { fill = Color.FromArgb(120, fill); fore = Theme.Muted; }
            Theme.FillRound(g, fill, border, r, Theme.S(7));

            var textRect = new Rectangle(0, 0, Width - (Chevron ? Theme.S(16) : 0), Height);
            TextRenderer.DrawText(g, Text, Font, textRect, fore,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix);
            if (Chevron)
            {
                float cx = Width - Theme.S(18), cy = Height / 2f;
                using (var p = new Pen(Theme.Muted, Math.Max(1.3f, 1.4f * Theme.Scale)))
                    g.DrawLines(p, new[] { new PointF(cx - Theme.S(4), cy - Theme.S(2)), new PointF(cx, cy + Theme.S(2)), new PointF(cx + Theme.S(4), cy - Theme.S(2)) });
            }
        }
    }

    // A button that opens a menu of choices, like a dropdown.
    internal sealed class ChoiceButton : Control
    {
        readonly FlatButton button = new FlatButton("", false);
        Choice[] choices = new Choice[0];
        string value = "";
        public event EventHandler Changed;

        public ChoiceButton()
        {
            button.Chevron = true;
            button.Dock = DockStyle.Fill;
            button.Click += delegate { Open(); };
            Controls.Add(button);
            Height = Theme.S(34);
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
                Choice c = Groq.Find(choices.Length > 0 ? choices : new[] { new Choice("", "", "") }, this.value);
                button.Text = c.Name;
                Width = button.PreferredWidth();
            }
        }

        public Choice Selected { get { return choices.Length > 0 ? Groq.Find(choices, value) : null; } }

        void Open()
        {
            var menu = Theme.Menu();
            menu.ShowImageMargin = true;
            foreach (var c in choices)
            {
                var choice = c;
                var item = Theme.Item(choice.Name, delegate
                {
                    Value = choice.Id;
                    if (Changed != null) Changed(this, EventArgs.Empty);
                });
                item.Checked = choice.Id == value;
                menu.Items.Add(item);
            }
            menu.Show(this, new Point(0, Height + Theme.S(4)));
        }
    }

    // A full-width settings row: title, description and an on/off switch.
    internal sealed class ToggleRow : Drawn
    {
        public string Title;
        public string Description;
        bool on;
        public event EventHandler Changed;
        readonly Font titleFont = Theme.F(10f);
        readonly Font descFont = Theme.F(9f);

        public ToggleRow(string title, string description)
        {
            Title = title;
            Description = description;
            Cursor = Cursors.Hand;
            TabStop = true;
        }

        public bool Checked
        {
            get { return on; }
            set { if (on != value) { on = value; Invalidate(); } }
        }

        int SwitchW { get { return Theme.S(40); } }
        int TextWidth(int width) { return Math.Max(Theme.S(120), width - SwitchW - Theme.S(24)); }

        public int MeasureHeight(int width)
        {
            int h = TextRenderer.MeasureText(Title, titleFont).Height;
            if (!string.IsNullOrEmpty(Description))
                h += Theme.S(2) + TextRenderer.MeasureText(Description, descFont, new Size(TextWidth(width), int.MaxValue),
                    TextFormatFlags.WordBreak | TextFormatFlags.NoPrefix | TextFormatFlags.TextBoxControl).Height;
            return h + Theme.S(24);
        }

        void Toggle()
        {
            on = !on;
            Invalidate();
            if (Changed != null) Changed(this, EventArgs.Empty);
        }

        protected override void OnClick(EventArgs e) { Focus(); Toggle(); base.OnClick(e); }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Space) Toggle();
            base.OnKeyDown(e);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(Parent != null ? Parent.BackColor : Theme.Bg);
            int pad = Theme.S(12);

            int tw = TextWidth(Width);
            int titleH = TextRenderer.MeasureText(Title, titleFont).Height;
            TextRenderer.DrawText(g, Title, titleFont, new Rectangle(0, pad, tw, titleH), Theme.Text,
                TextFormatFlags.NoPrefix | TextFormatFlags.Left);
            if (!string.IsNullOrEmpty(Description))
                TextRenderer.DrawText(g, Description, descFont, new Rectangle(0, pad + titleH + Theme.S(2), tw, Height), Theme.Muted,
                    TextFormatFlags.WordBreak | TextFormatFlags.NoPrefix | TextFormatFlags.TextBoxControl);

            int sw = SwitchW, sh = Theme.S(22);
            var track = new RectangleF(Width - sw - Theme.S(2), (Height - sh) / 2f, sw, sh);
            Color trackColor = on ? (Hover ? Theme.AccentHover : Theme.Accent) : Theme.Track;
            Theme.FillRound(g, trackColor, Focused ? Theme.Accent : Color.Empty, track, sh / 2f);
            float knob = sh - Theme.S(6);
            float kx = on ? track.Right - knob - Theme.S(3) : track.Left + Theme.S(3);
            using (var b = new SolidBrush(Color.White))
                g.FillEllipse(b, kx, track.Top + Theme.S(3), knob, knob);

            using (var p = new Pen(Theme.Border, 1))
                g.DrawLine(p, 0, Height - 1, Width, Height - 1);
        }
    }

    // A text box with a soft rounded border that turns accent on focus.
    internal sealed class InputBox : Control
    {
        public readonly TextBox Box = new TextBox();

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, string lParam);

        public InputBox(bool multiline)
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.ResizeRedraw, true);
            Box.BorderStyle = BorderStyle.None;
            Box.Multiline = multiline;
            Box.Font = Theme.F(10f);
            Box.BackColor = Theme.Surface;
            Box.ForeColor = Theme.Text;
            if (multiline) Box.ScrollBars = ScrollBars.None;
            Box.GotFocus += delegate { Invalidate(); };
            Box.LostFocus += delegate { Invalidate(); };
            Controls.Add(Box);
            Height = Box.PreferredHeight + Theme.S(18);
            Cursor = Cursors.IBeam;
        }

        public void Cue(string text)
        {
            Box.HandleCreated += delegate { SendMessage(Box.Handle, 0x1501, (IntPtr)1, text); };
            if (Box.IsHandleCreated) SendMessage(Box.Handle, 0x1501, (IntPtr)1, text);
        }

        protected override void OnLayout(LayoutEventArgs e)
        {
            int px = Theme.S(11);
            if (Box.Multiline)
                Box.Bounds = new Rectangle(px, Theme.S(8), Width - px * 2, Height - Theme.S(14));
            else
                Box.Bounds = new Rectangle(px, (Height - Box.PreferredHeight) / 2 + 1, Width - px * 2, Box.PreferredHeight);
            base.OnLayout(e);
        }

        protected override void OnClick(EventArgs e) { Box.Focus(); base.OnClick(e); }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(Parent != null ? Parent.BackColor : Theme.Bg);
            Theme.FillRound(g, Theme.Surface, Box.Focused ? Theme.Accent : Theme.Border, new RectangleF(0, 0, Width, Height), Theme.S(7));
        }
    }

    // Header tab: plain text with an accent underline when active.
    internal sealed class NavTab : Drawn
    {
        bool active;
        public NavTab(string text)
        {
            Text = text;
            Font = Theme.F(10f);
            Cursor = Cursors.Hand;
            Size = new Size(TextRenderer.MeasureText(text, Font).Width + Theme.S(20), Theme.S(40));
        }

        public bool Active
        {
            get { return active; }
            set { active = value; Invalidate(); }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(Parent != null ? Parent.BackColor : Theme.Bg);
            Color c = active || Hover ? Theme.Text : Theme.Muted;
            TextRenderer.DrawText(g, Text, Font, new Rectangle(0, 0, Width, Height - Theme.S(6)), c,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix);
            if (active)
                Theme.FillRound(g, Theme.Accent, Color.Empty,
                    new RectangleF(Theme.S(10), Height - Theme.S(5), Width - Theme.S(20), Theme.S(3)), Theme.S(1.5f));
        }
    }
}
