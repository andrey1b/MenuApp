using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Windows.Forms;

namespace MenuApp
{
    class RoundedButton : Button
    {
        [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
        public int CornerRadius { get; set; } = 8;

        public RoundedButton()
        {
            SetStyle(
                ControlStyles.UserPaint |
                ControlStyles.AllPaintingInWmPaint |
                ControlStyles.OptimizedDoubleBuffer, true);
            FlatStyle = FlatStyle.Flat;
            FlatAppearance.BorderSize = 0;
            Cursor = Cursors.Hand;
        }

        private bool _hover, _pressed;

        protected override void OnMouseEnter(EventArgs e) { _hover = true;  Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { _hover = false; Invalidate(); base.OnMouseLeave(e); }
        protected override void OnMouseDown(MouseEventArgs e) { if (e.Button == MouseButtons.Left) { _pressed = true;  Invalidate(); } base.OnMouseDown(e); }
        protected override void OnMouseUp(MouseEventArgs e)   { _pressed = false; Invalidate(); base.OnMouseUp(e); }
        protected override void OnEnabledChanged(EventArgs e) { Invalidate(); base.OnEnabledChanged(e); }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode     = SmoothingMode.AntiAlias;
            g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;

            Color bg = Enabled ? BackColor : Color.FromArgb(160, 160, 160);
            if (Enabled && _pressed)    bg = Shift(bg, -22);
            else if (Enabled && _hover) bg = Shift(bg, +15);

            var rect = new Rectangle(0, 0, Width - 1, Height - 1);
            using var path  = RoundedPath(rect, CornerRadius);
            using var brush = new SolidBrush(bg);
            g.FillPath(brush, path);

            Color fg = Enabled ? ForeColor : Color.FromArgb(210, 210, 210);
            var sf = new StringFormat
            {
                Alignment     = StringAlignment.Center,
                LineAlignment = StringAlignment.Center,
                FormatFlags   = StringFormatFlags.NoWrap
            };
            using var fgBrush = new SolidBrush(fg);
            g.DrawString(Text, Font, fgBrush, new RectangleF(0, 0, Width, Height), sf);
        }

        private static GraphicsPath RoundedPath(Rectangle r, int radius)
        {
            int d = radius * 2;
            var p = new GraphicsPath();
            p.AddArc(r.Left,      r.Top,          d, d, 180, 90);
            p.AddArc(r.Right - d, r.Top,          d, d, 270, 90);
            p.AddArc(r.Right - d, r.Bottom - d,   d, d,   0, 90);
            p.AddArc(r.Left,      r.Bottom - d,   d, d,  90, 90);
            p.CloseFigure();
            return p;
        }

        private static Color Shift(Color c, int delta) => Color.FromArgb(
            c.A,
            Math.Clamp(c.R + delta, 0, 255),
            Math.Clamp(c.G + delta, 0, 255),
            Math.Clamp(c.B + delta, 0, 255));
    }
}
