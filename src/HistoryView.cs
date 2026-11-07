using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace Yappity
{
    // The history list, drawn as cards. Click a card to open it and see its
    // actions; double-click to copy.
    internal sealed class HistoryView : Drawn
    {
        sealed class Card
        {
            public HistoryEntry Entry;
            public Rectangle Bounds;
            public Rectangle TextBounds;
            public bool Clipped;
            public readonly List<KeyValuePair<string, Rectangle>> Actions = new List<KeyValuePair<string, Rectangle>>();
        }

        public List<HistoryEntry> Items = new List<HistoryEntry>();
        public string HowTo = "";
        public event Action<HistoryEntry, string> ActionClicked;

        readonly List<Card> cards = new List<Card>();
        readonly Font timeFont = Theme.F(8.5f);
        readonly Font textFont = Theme.F(10.5f);
        readonly Font actionFont = Theme.F(9f);
        readonly Font emptyTitle = Theme.Bold(13f);
        readonly Font emptyBody = Theme.F(10f);
        readonly Timer toastTimer = new Timer();
        Bitmap emptyIcon;
        string selectedId;
        string toast;
        int scroll;
        int contentHeight;
        Point mouse = new Point(-1, -1);

        const TextFormatFlags Wrap = TextFormatFlags.WordBreak | TextFormatFlags.NoPrefix | TextFormatFlags.TextBoxControl | TextFormatFlags.NoPadding;

        public HistoryView()
        {
            BackColor = Theme.Bg;
            TabStop = true;
            toastTimer.Interval = 1300;
            toastTimer.Tick += delegate { toastTimer.Stop(); toast = null; Invalidate(); };
        }

        public void SetItems(List<HistoryEntry> items)
        {
            Items = items;
            Relayout();
        }

        public void Toast(string text)
        {
            toast = text;
            toastTimer.Stop();
            toastTimer.Start();
            Invalidate();
        }

        protected override void OnResize(EventArgs e) { base.OnResize(e); Relayout(); }

        void Relayout()
        {
            cards.Clear();
            int side = Theme.S(20), pad = Theme.S(14), gap = Theme.S(8);
            int w = Math.Max(Theme.S(200), Width - side * 2);
            int textW = w - pad * 2;
            int y = Theme.S(4);
            int lineH = TextRenderer.MeasureText("Ag", textFont).Height;
            int timeH = TextRenderer.MeasureText("Ag", timeFont).Height;
            int actionH = TextRenderer.MeasureText("Ag", actionFont).Height + Theme.S(4);

            foreach (var e in Items)
            {
                var c = new Card();
                c.Entry = e;
                bool open = e.Id == selectedId;
                string body = Body(e);
                int full = TextRenderer.MeasureText(body, textFont, new Size(textW, int.MaxValue), Wrap).Height;
                int maxClosed = lineH * 3;
                int textH = open ? full : Math.Min(full, maxClosed);
                c.Clipped = !open && full > maxClosed;

                int h = pad + timeH + Theme.S(6) + textH + pad;
                if (open) h += Theme.S(10) + actionH;
                c.Bounds = new Rectangle(side, y, w, h);
                c.TextBounds = new Rectangle(side + pad, y + pad + timeH + Theme.S(6), textW, textH);

                if (open)
                {
                    int ax = side + pad, ay = c.TextBounds.Bottom + Theme.S(10);
                    foreach (string a in ActionsFor(e))
                    {
                        int aw = TextRenderer.MeasureText(a, actionFont).Width;
                        c.Actions.Add(new KeyValuePair<string, Rectangle>(a, new Rectangle(ax, ay, aw, actionH)));
                        ax += aw + Theme.S(18);
                    }
                }
                cards.Add(c);
                y += h + gap;
            }
            contentHeight = y + Theme.S(12);
            ClampScroll();
            Invalidate();
        }

        static string Body(HistoryEntry e)
        {
            if (e.Failed) return e.Error ?? "Failed";
            return e.Text ?? "";
        }

        static IEnumerable<string> ActionsFor(HistoryEntry e)
        {
            if (e.Failed)
            {
                if (!string.IsNullOrEmpty(e.AudioFile)) yield return "Retry";
            }
            else
            {
                yield return "Copy";
                if (e.Raw != e.Text) yield return "Copy original";
            }
            yield return "Delete";
        }

        void ClampScroll()
        {
            int max = Math.Max(0, contentHeight - Height);
            scroll = Math.Max(0, Math.Min(scroll, max));
        }

        protected override void OnMouseWheel(MouseEventArgs e)
        {
            scroll -= e.Delta * Theme.S(64) / 120;
            ClampScroll();
            Invalidate();
            base.OnMouseWheel(e);
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            if (e.KeyCode == Keys.PageDown) { scroll += Height - Theme.S(40); ClampScroll(); Invalidate(); }
            if (e.KeyCode == Keys.PageUp) { scroll -= Height - Theme.S(40); ClampScroll(); Invalidate(); }
            if (e.KeyCode == Keys.Home) { scroll = 0; Invalidate(); }
            base.OnKeyDown(e);
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            mouse = e.Location;
            Cursor = HitCard(mouse) != null ? Cursors.Hand : Cursors.Default;
            Invalidate();
            base.OnMouseMove(e);
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            mouse = new Point(-1, -1);
            base.OnMouseLeave(e);
        }

        Card HitCard(Point p)
        {
            var q = new Point(p.X, p.Y + scroll);
            foreach (var c in cards) if (c.Bounds.Contains(q)) return c;
            return null;
        }

        protected override void OnMouseClick(MouseEventArgs e)
        {
            Focus();
            if (e.Button != MouseButtons.Left) return;
            var c = HitCard(e.Location);
            if (c == null) return;
            var q = new Point(e.X, e.Y + scroll);
            foreach (var a in c.Actions)
            {
                var hit = a.Value;
                hit.Inflate(Theme.S(6), Theme.S(4));
                if (hit.Contains(q))
                {
                    if (ActionClicked != null) ActionClicked(c.Entry, a.Key);
                    return;
                }
            }
            selectedId = selectedId == c.Entry.Id ? null : c.Entry.Id;
            Relayout();
            // Keep the opened card fully visible.
            foreach (var k in cards)
                if (k.Entry.Id == selectedId && k.Bounds.Bottom - scroll > Height)
                {
                    scroll = k.Bounds.Bottom - Height + Theme.S(12);
                    ClampScroll();
                }
            base.OnMouseClick(e);
        }

        protected override void OnMouseDoubleClick(MouseEventArgs e)
        {
            var c = HitCard(e.Location);
            if (c != null && !c.Entry.Failed && ActionClicked != null) ActionClicked(c.Entry, "Copy");
            base.OnMouseDoubleClick(e);
        }

        protected override void OnPaint(PaintEventArgs pe)
        {
            var g = pe.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(Theme.Bg);

            if (Items.Count == 0) { PaintEmpty(g); return; }

            var q = new Point(mouse.X, mouse.Y + scroll);
            foreach (var c in cards)
            {
                var b = c.Bounds;
                b.Offset(0, -scroll);
                if (b.Bottom < 0 || b.Top > Height) continue;
                bool open = c.Entry.Id == selectedId;
                bool hover = c.Bounds.Contains(q);
                Theme.FillRound(g, hover && !open ? Theme.SurfaceHover : Theme.Surface,
                    open ? Theme.Accent : Theme.Border, b, Theme.S(10));

                // Time and status line
                var e = c.Entry;
                var local = e.TimeUtc.ToLocalTime();
                string when = local.Date == DateTime.Today ? local.ToString("h:mm tt") :
                    local.Date == DateTime.Today.AddDays(-1) ? "Yesterday, " + local.ToString("h:mm tt") : local.ToString("MMM d, h:mm tt");
                if (e.Seconds > 0) when += "  ·  " + FormatSeconds(e.Seconds);
                int pad = Theme.S(14);
                var timeRect = new Rectangle(b.Left + pad, b.Top + pad, b.Width - pad * 2, timeFont.Height + Theme.S(2));
                TextRenderer.DrawText(g, when, timeFont, timeRect, Theme.Muted, TextFormatFlags.NoPrefix | TextFormatFlags.Left | TextFormatFlags.NoPadding);
                if (e.Failed)
                {
                    int tw = TextRenderer.MeasureText(when, timeFont).Width;
                    var badge = new Rectangle(timeRect.Left + tw + Theme.S(6), timeRect.Top - Theme.S(1),
                        TextRenderer.MeasureText("Didn't send", timeFont).Width + Theme.S(12), timeFont.Height + Theme.S(3));
                    Theme.FillRound(g, Color.FromArgb(Theme.Dark ? 50 : 30, Theme.Warn), Color.Empty, badge, badge.Height / 2f);
                    TextRenderer.DrawText(g, "Didn't send", timeFont, badge, Theme.Warn,
                        TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix);
                }

                var tr = c.TextBounds;
                tr.Offset(0, -scroll);
                TextRenderer.DrawText(g, Body(e), textFont, tr, e.Failed ? Theme.Muted : Theme.Text,
                    Wrap | (c.Clipped ? TextFormatFlags.EndEllipsis : 0));

                foreach (var a in c.Actions)
                {
                    var ar = a.Value;
                    bool over = ar.Contains(q);
                    ar.Offset(0, -scroll);
                    Color col = a.Key == "Delete" ? Theme.Muted : Theme.Accent;
                    if (over) col = a.Key == "Delete" ? Theme.Danger : Theme.AccentHover;
                    TextRenderer.DrawText(g, a.Key, actionFont, ar, col, TextFormatFlags.NoPrefix | TextFormatFlags.Left | TextFormatFlags.VerticalCenter);
                }
            }

            // Slim scrollbar
            if (contentHeight > Height)
            {
                float thumbH = Math.Max(Theme.S(32), Height * (float)Height / contentHeight);
                float thumbY = scroll * (Height - thumbH) / (contentHeight - Height);
                Theme.FillRound(g, Theme.Border, Color.Empty, new RectangleF(Width - Theme.S(8), thumbY + Theme.S(2), Theme.S(4), thumbH - Theme.S(4)), Theme.S(2));
            }

            if (toast != null)
            {
                var size = TextRenderer.MeasureText(toast, actionFont);
                var r = new Rectangle((Width - size.Width) / 2 - Theme.S(16), Height - Theme.S(52), size.Width + Theme.S(32), Theme.S(32));
                Theme.FillRound(g, Theme.Text, Color.Empty, r, r.Height / 2f);
                TextRenderer.DrawText(g, toast, actionFont, r, Theme.Bg,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix);
            }
        }

        static string FormatSeconds(double s)
        {
            if (s < 60) return Math.Max(1, (int)Math.Round(s)) + "s";
            return ((int)s / 60) + "m " + ((int)s % 60) + "s";
        }

        void PaintEmpty(Graphics g)
        {
            int iconSize = Theme.S(56);
            if (emptyIcon == null) emptyIcon = Icons.Draw(iconSize, Icons.IdleColor);
            int cy = Height / 2 - Theme.S(50);
            g.DrawImage(emptyIcon, (Width - iconSize) / 2, cy - iconSize / 2);
            var titleRect = new Rectangle(0, cy + iconSize / 2 + Theme.S(18), Width, emptyTitle.Height + Theme.S(4));
            TextRenderer.DrawText(g, "No yaps yet", emptyTitle, titleRect, Theme.Text,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.NoPrefix);
            var bodyRect = new Rectangle(Theme.S(40), titleRect.Bottom + Theme.S(6), Width - Theme.S(80), Theme.S(60));
            TextRenderer.DrawText(g, HowTo + "\nWhatever you say gets typed where your cursor is, and it shows up here too.",
                emptyBody, bodyRect, Theme.Muted, TextFormatFlags.HorizontalCenter | Wrap);
        }
    }
}
