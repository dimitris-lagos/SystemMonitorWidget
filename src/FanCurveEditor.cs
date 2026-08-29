using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace VegaDesktopWidget
{
    internal sealed class FanCurveEditor : Control
    {
        private List<FanCurvePoint> points = FanProfile.DefaultPoints(); private int dragIndex = -1;
        public event EventHandler CurveChanged;
        public List<FanCurvePoint> Points
        {
            get { List<FanCurvePoint> result = new List<FanCurvePoint>(); foreach (FanCurvePoint point in points) result.Add(point.Clone()); return result; }
            set { points = new FanProfile { Points = value ?? FanProfile.DefaultPoints() }.NormalizedPoints(); Invalidate(); }
        }

        public FanCurveEditor()
        {
            DoubleBuffered = true; BackColor = Color.FromArgb(20, 27, 36); ForeColor = Color.White; MinimumSize = new Size(360, 260); Cursor = Cursors.Cross;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e); Graphics g = e.Graphics; g.SmoothingMode = SmoothingMode.AntiAlias; Rectangle plot = PlotRectangle;
            using (SolidBrush background = new SolidBrush(Color.FromArgb(15, 21, 29))) g.FillRectangle(background, plot);
            using (Pen grid = new Pen(Color.FromArgb(44, 57, 72)))
            {
                for (int i = 0; i <= 4; i++) { int x = plot.Left + i * plot.Width / 4; g.DrawLine(grid, x, plot.Top, x, plot.Bottom); int y = plot.Top + i * plot.Height / 4; g.DrawLine(grid, plot.Left, y, plot.Right, y); }
            }
            using (Font axis = new Font("Segoe UI", 8f)) using (SolidBrush label = new SolidBrush(Color.FromArgb(145, 160, 179)))
            {
                for (int i = 0; i <= 4; i++) { string t = (20 + i * 20) + "°"; SizeF size = g.MeasureString(t, axis); g.DrawString(t, axis, label, plot.Left + i * plot.Width / 4 - size.Width / 2, plot.Bottom + 7); string p = (100 - i * 25) + "%"; size = g.MeasureString(p, axis); g.DrawString(p, axis, label, plot.Left - size.Width - 8, plot.Top + i * plot.Height / 4 - size.Height / 2); }
            }
            PointF[] curve = new PointF[4]; for (int i = 0; i < 4; i++) curve[i] = ToScreen(points[i], plot);
            using (Pen shadow = new Pen(Color.FromArgb(55, 0, 0, 0), 7f)) g.DrawLines(shadow, curve);
            using (Pen line = new Pen(Color.FromArgb(65, 215, 145), 3f)) g.DrawLines(line, curve);
            for (int i = 0; i < curve.Length; i++)
            {
                Color color = i == dragIndex ? Color.FromArgb(255, 193, 92) : Color.FromArgb(75, 225, 155);
                using (SolidBrush fill = new SolidBrush(color)) g.FillEllipse(fill, curve[i].X - 7, curve[i].Y - 7, 14, 14);
                using (Pen border = new Pen(Color.White, 1.5f)) g.DrawEllipse(border, curve[i].X - 7, curve[i].Y - 7, 14, 14);
                string text = points[i].Temperature + "° / " + points[i].Percent + "%";
                using (Font font = new Font("Segoe UI", 8f, FontStyle.Bold)) using (SolidBrush brush = new SolidBrush(Color.FromArgb(220, 229, 239)))
                { SizeF size = g.MeasureString(text, font); float x = Math.Max(plot.Left, Math.Min(plot.Right - size.Width, curve[i].X - size.Width / 2)); float y = curve[i].Y < plot.Top + 28 ? curve[i].Y + 10 : curve[i].Y - size.Height - 10; g.DrawString(text, font, brush, x, y); }
            }
            using (Font title = new Font("Segoe UI", 9f, FontStyle.Bold)) using (SolidBrush brush = new SolidBrush(Color.FromArgb(205, 216, 231))) g.DrawString("FAN OUTPUT", title, brush, plot.Left, 8);
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e); if (e.Button != MouseButtons.Left) return; Rectangle plot = PlotRectangle; float best = 18f; dragIndex = -1;
            for (int i = 0; i < points.Count; i++) { PointF p = ToScreen(points[i], plot); float distance = (float)Math.Sqrt((p.X - e.X) * (p.X - e.X) + (p.Y - e.Y) * (p.Y - e.Y)); if (distance < best) { best = distance; dragIndex = i; } }
            if (dragIndex >= 0) { Capture = true; UpdateDrag(e.Location); }
        }
        protected override void OnMouseMove(MouseEventArgs e) { base.OnMouseMove(e); if (dragIndex >= 0 && Capture) UpdateDrag(e.Location); }
        protected override void OnMouseUp(MouseEventArgs e) { base.OnMouseUp(e); if (dragIndex >= 0) { UpdateDrag(e.Location); dragIndex = -1; Capture = false; Invalidate(); } }

        private void UpdateDrag(Point location)
        {
            Rectangle plot = PlotRectangle; int temperature = (int)Math.Round(20 + 80.0 * (location.X - plot.Left) / Math.Max(1, plot.Width)); int percent = (int)Math.Round(100.0 * (plot.Bottom - location.Y) / Math.Max(1, plot.Height));
            temperature = Math.Max(20, Math.Min(100, temperature)); percent = Math.Max(20, Math.Min(100, percent));
            if (dragIndex > 0) { temperature = Math.Max(points[dragIndex - 1].Temperature + 1, temperature); percent = Math.Max(points[dragIndex - 1].Percent, percent); }
            if (dragIndex < 3) { temperature = Math.Min(points[dragIndex + 1].Temperature - 1, temperature); percent = Math.Min(points[dragIndex + 1].Percent, percent); }
            points[dragIndex].Temperature = temperature; points[dragIndex].Percent = percent; Invalidate(); EventHandler handler = CurveChanged; if (handler != null) handler(this, EventArgs.Empty);
        }
        private Rectangle PlotRectangle { get { return new Rectangle(54, 34, Math.Max(100, Width - 76), Math.Max(100, Height - 78)); } }
        private static PointF ToScreen(FanCurvePoint point, Rectangle plot) { float x = plot.Left + (point.Temperature - 20) / 80f * plot.Width; float y = plot.Bottom - point.Percent / 100f * plot.Height; return new PointF(Math.Max(plot.Left, Math.Min(plot.Right, x)), Math.Max(plot.Top, Math.Min(plot.Bottom, y))); }
    }
}