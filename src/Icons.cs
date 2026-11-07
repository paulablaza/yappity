using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;

namespace Yappity
{
    // Draws the microphone icon in code so the app ships as a single file.
    internal static class Icons
    {
        public static readonly Color IdleColor = Color.FromArgb(79, 70, 229);     // indigo
        public static readonly Color RecordingColor = Color.FromArgb(220, 38, 38); // red
        public static readonly Color WorkingColor = Color.FromArgb(217, 119, 6);   // amber

        public static Bitmap Draw(int size, Color background)
        {
            var bmp = new Bitmap(size, size, PixelFormat.Format32bppArgb);
            using (var g = Graphics.FromImage(bmp))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.Clear(Color.Transparent);
                float s = size / 32f;

                using (var bg = new SolidBrush(background))
                using (var path = Rounded(new RectangleF(0.5f * s, 0.5f * s, 31 * s, 31 * s), 8 * s))
                    g.FillPath(bg, path);

                using (var white = new SolidBrush(Color.White))
                using (var pen = new Pen(Color.White, 2.4f * s))
                using (var capsule = Rounded(new RectangleF(12 * s, 5 * s, 8 * s, 14 * s), 4 * s))
                {
                    pen.StartCap = LineCap.Round;
                    pen.EndCap = LineCap.Round;
                    g.FillPath(white, capsule);
                    g.DrawArc(pen, 8 * s, 9.5f * s, 16 * s, 13 * s, 0, 180);
                    g.DrawLine(pen, 16 * s, 22.5f * s, 16 * s, 26.5f * s);
                }
            }
            return bmp;
        }

        public static Icon Make(int size, Color background)
        {
            using (var bmp = Draw(size, background))
                return Icon.FromHandle(bmp.GetHicon());
        }

        // Tray icon: Yappy-chan's wink, with a small red (recording) or amber
        // (working) dot in the corner. Falls back to the drawn mic if the image is missing.
        public static Icon Tray(int size, Color dot)
        {
            Bitmap face = null;
            try
            {
                var s = System.Reflection.Assembly.GetExecutingAssembly().GetManifestResourceStream("tray.png");
                if (s != null) face = new Bitmap(s);
            }
            catch (Exception) { }
            if (face == null) return Make(size, dot.IsEmpty ? IdleColor : dot);

            using (face)
            using (var bmp = new Bitmap(size, size, PixelFormat.Format32bppArgb))
            {
                using (var g = Graphics.FromImage(bmp))
                {
                    g.SmoothingMode = SmoothingMode.AntiAlias;
                    g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                    g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                    g.Clear(Color.Transparent);
                    g.DrawImage(face, 0, 0, size, size);
                    if (!dot.IsEmpty)
                    {
                        float d = size * 0.3f;
                        float x = size - d - 0.5f, y = size - d - 0.5f;
                        using (var ring = new SolidBrush(Color.FromArgb(24, 24, 28))) g.FillEllipse(ring, x - size * 0.05f, y - size * 0.05f, d + size * 0.1f, d + size * 0.1f);
                        using (var b = new SolidBrush(dot)) g.FillEllipse(b, x, y, d, d);
                    }
                }
                return Icon.FromHandle(bmp.GetHicon());
            }
        }

        static GraphicsPath Rounded(RectangleF r, float radius)
        {
            float d = radius * 2;
            var p = new GraphicsPath();
            p.AddArc(r.Left, r.Top, d, d, 180, 90);
            p.AddArc(r.Right - d, r.Top, d, d, 270, 90);
            p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            p.AddArc(r.Left, r.Bottom - d, d, d, 90, 90);
            p.CloseFigure();
            return p;
        }

        // Writes a multi-size .ico (PNG entries) for the .exe. Used by build.ps1.
        public static void WriteIco(string path)
        {
            int[] sizes = { 16, 20, 24, 32, 48, 64, 256 };
            var pngs = new byte[sizes.Length][];
            for (int i = 0; i < sizes.Length; i++)
            {
                using (var bmp = Draw(sizes[i], IdleColor))
                using (var ms = new MemoryStream())
                {
                    bmp.Save(ms, ImageFormat.Png);
                    pngs[i] = ms.ToArray();
                }
            }
            using (var fs = File.Create(path))
            using (var w = new BinaryWriter(fs))
            {
                w.Write((short)0);
                w.Write((short)1);
                w.Write((short)sizes.Length);
                int offset = 6 + 16 * sizes.Length;
                for (int i = 0; i < sizes.Length; i++)
                {
                    w.Write((byte)(sizes[i] >= 256 ? 0 : sizes[i]));
                    w.Write((byte)(sizes[i] >= 256 ? 0 : sizes[i]));
                    w.Write((byte)0);
                    w.Write((byte)0);
                    w.Write((short)1);
                    w.Write((short)32);
                    w.Write(pngs[i].Length);
                    w.Write(offset);
                    offset += pngs[i].Length;
                }
                foreach (var png in pngs) w.Write(png);
            }
        }
    }
}
