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
            K.Load(); // the WPF window and bubble use the same light/dark choice
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
}
