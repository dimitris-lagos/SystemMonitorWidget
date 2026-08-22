using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace VegaDesktopWidget
{
    internal sealed class MetricExtrema
    {
        public bool HasValue;
        public double Minimum;
        public double Maximum;

        public void Add(double value)
        {
            if (!HasValue) { Minimum = value; Maximum = value; HasValue = true; return; }
            if (value < Minimum) Minimum = value;
            if (value > Maximum) Maximum = value;
        }
    }

    internal sealed class ComponentGrid
    {
        public const int Gap = 8;
        public const int FullCellHeight = 64;
        public const int GraphHeight = 53;

        private readonly Rectangle bounds;
        private readonly int columns;
        private readonly int cellWidth;

        public ComponentGrid(Rectangle bounds, int columns)
        {
            this.bounds = bounds;
            this.columns = columns == 3 ? 3 : 4;
            cellWidth = (bounds.Width - Gap * (this.columns - 1)) / this.columns;
        }

        public Rectangle Cell(int column, int y, int height)
        {
            int x = bounds.X + column * (cellWidth + Gap);
            int width = column == columns - 1 ? bounds.Right - x : cellWidth;
            return new Rectangle(x, y, width, height);
        }

        public Rectangle Span(int firstColumn, int columnCount, int y, int height)
        {
            Rectangle first = Cell(firstColumn, y, height);
            Rectangle last = Cell(firstColumn + columnCount - 1, y, height);
            return Rectangle.FromLTRB(first.Left, y, last.Right, y + height);
        }

        public Rectangle HalfCell(Rectangle cell, int half)
        {
            int height = (cell.Height - Gap) / 2;
            return new Rectangle(cell.X, cell.Y + half * (height + Gap), cell.Width, height);
        }
    }
    internal sealed class WidgetComponents
    {
        private static readonly Color BoxBackground = Color.FromArgb(18, 23, 31);
        private static readonly Color LabelColor = Color.FromArgb(129, 143, 160);
        private static readonly Color SecondaryColor = Color.FromArgb(105, 117, 132);

        public void DrawBigMetric(Graphics g, Rectangle r, string label, string current, string maximum, string minimum, Color accent, bool showExtrema)
        {
            FillBox(g, r);
            if (showExtrema)
            {
                DrawText(g, label, 6.7f, FontStyle.Bold, LabelColor, new RectangleF(r.X + 7, r.Y + 4, r.Width - 14, 14), StringAlignment.Near);
                DrawValueText(g, current, current.Length > 7 ? 11.5f : current.Length > 4 ? 12.5f : 17f, FontStyle.Bold, accent, new RectangleF(r.X + 3, r.Y + 21, r.Width - 6, r.Height - 25), StringAlignment.Near);
                DrawValueText(g, "↑ " + maximum, 9f, FontStyle.Bold, SecondaryColor, new RectangleF(r.X + 3, r.Y + 21, r.Width - 6, 13), StringAlignment.Far);
                DrawValueText(g, "↓ " + minimum, 9f, FontStyle.Bold, SecondaryColor, new RectangleF(r.X + 3, r.Y + 36, r.Width - 6, 13), StringAlignment.Far);
            }
            else
            {
                DrawText(g, label, 6.7f, FontStyle.Bold, LabelColor, new RectangleF(r.X + 7, r.Y + 6, r.Width - 14, 15), StringAlignment.Near);
                DrawValueText(g, current, current.Length > 7 ? 11.5f : current.Length > 4 ? 14.5f : 18f, FontStyle.Bold, accent, new RectangleF(r.X + 2, r.Y + 20, r.Width - 4, r.Height - 22), StringAlignment.Near);
            }
        }
        public void DrawHorizontalSpec(Graphics g, Rectangle r, string label, string value, Color accent)
        {
            FillBox(g, r);
            DrawText(g, label, 6.2f, FontStyle.Bold, LabelColor, new RectangleF(r.X + 3, r.Y + 2, r.Width - 6, r.Height - 4), StringAlignment.Near);
            DrawValueText(g, value, value.Length > 4 ? 10.5f : 12.5f, FontStyle.Bold, accent, new RectangleF(r.X + 3, r.Y + 2, r.Width - 6, r.Height - 4), StringAlignment.Far);
        }

        public void DrawVerticalSpec(Graphics g, Rectangle r, string label, string value, Color accent)
        {
            FillBox(g, r);
            DrawText(g, label, 7f, FontStyle.Bold, LabelColor, new RectangleF(r.X + 7, r.Y + 6, r.Width - 14, 14), StringAlignment.Near);
            DrawValueText(g, value, value.Length > 5 ? 11.5f : 14.5f, FontStyle.Bold, accent, new RectangleF(r.X, r.Y + 20, r.Width, r.Height - 23), StringAlignment.Near);
        }

        public void DrawGraphBox(Graphics g, Rectangle r, string label, string current, string rangeText, Color accent, IList<double> values, double minimum, double maximum)
        {
            FillBox(g, r);
            DrawText(g, label, 7f, FontStyle.Bold, LabelColor, new RectangleF(r.X + 7, r.Y + 5, 145, 15), StringAlignment.Near);
            DrawValueText(g, rangeText, 8.5f, FontStyle.Regular, SecondaryColor, new RectangleF(r.Right - 75, r.Y + 5, 68, 15), StringAlignment.Far);
            DrawValueText(g, current, 15f, FontStyle.Bold, accent, new RectangleF(r.X + 7, r.Y + 21, 95, 28), StringAlignment.Near);
            DrawSpark(g, new Rectangle(r.X + 112, r.Y + 20, r.Width - 120, r.Height - 27), accent, values, minimum, maximum);
        }

        private static void FillBox(Graphics g, Rectangle r)
        {
            using (SolidBrush b = new SolidBrush(BoxBackground)) g.FillRectangle(b, r);
        }

        private static void DrawSpark(Graphics g, Rectangle r, Color accent, IList<double> values, double minimum, double maximum)
        {
            if (values == null || values.Count < 2 || r.Width <= 0 || r.Height <= 0) return;
            double range = Math.Max(1, maximum - minimum);
            PointF[] points = new PointF[values.Count];
            float step = r.Width / (float)Math.Max(1, values.Count - 1);
            for (int i = 0; i < values.Count; i++)
            {
                double normalized = Math.Max(0, Math.Min(1, (values[i] - minimum) / range));
                points[i] = new PointF(r.X + i * step, r.Bottom - (float)normalized * r.Height);
            }
            using (Pen p = new Pen(Color.FromArgb(65, accent), 1))
            {
                g.DrawLine(p, r.X, r.Bottom, r.Right, r.Bottom);
                g.DrawLine(p, r.X, r.Y + r.Height / 2, r.Right, r.Y + r.Height / 2);
                g.DrawLine(p, r.X, r.Y, r.Right, r.Y);
            }
            using (Pen p = new Pen(accent, 1.8f)) g.DrawLines(p, points);
        }

        private static readonly string[] ValueUnits = { " RPM", " GHz", " MHz", " GB", " MB", "°C", " °C", " W", " V", " A", "°", "%" };

        private static void DrawValueText(Graphics g, string text, float size, FontStyle style, Color color, RectangleF area, StringAlignment align)
        {
            string main = text ?? String.Empty;
            string unit = String.Empty;
            foreach (string candidate in ValueUnits)
            {
                if (main.EndsWith(candidate, StringComparison.OrdinalIgnoreCase))
                {
                    unit = main.Substring(main.Length - candidate.Length);
                    main = main.Substring(0, main.Length - candidate.Length);
                    break;
                }
            }
            if (unit.Length == 0)
            {
                DrawText(g, text, size, style, color, area, align);
                return;
            }

            float unitSize = Math.Max(5f, size - 2f);
            using (Font mainFont = new Font("Segoe UI", size, style, GraphicsUnit.Point))
            using (Font unitFont = new Font("Segoe UI", unitSize, style, GraphicsUnit.Point))
            using (SolidBrush brush = new SolidBrush(color))
            using (StringFormat format = new StringFormat(StringFormat.GenericTypographic))
            {
                format.FormatFlags |= StringFormatFlags.NoWrap | StringFormatFlags.MeasureTrailingSpaces;
                float mainWidth = g.MeasureString(main, mainFont, Int32.MaxValue, format).Width;
                float unitWidth = g.MeasureString(unit, unitFont, Int32.MaxValue, format).Width;
                float totalWidth = mainWidth + unitWidth;
                float x = align == StringAlignment.Far ? area.Right - totalWidth : align == StringAlignment.Center ? area.X + (area.Width - totalWidth) / 2f : area.X;
                float mainHeight = mainFont.GetHeight(g);
                float unitHeight = unitFont.GetHeight(g);
                float mainY = area.Y + (area.Height - mainHeight) / 2f;
                float unitY = mainY + (mainHeight - unitHeight) * 0.72f;
                g.DrawString(main, mainFont, brush, new PointF(x, mainY), format);
                g.DrawString(unit, unitFont, brush, new PointF(x + mainWidth, unitY), format);
            }
        }
        private static void DrawText(Graphics g, string text, float size, FontStyle style, Color color, RectangleF area, StringAlignment align)
        {
            using (Font f = new Font("Segoe UI", size, style, GraphicsUnit.Point))
            using (SolidBrush b = new SolidBrush(color))
            using (StringFormat sf = new StringFormat())
            {
                sf.Alignment = align; sf.LineAlignment = StringAlignment.Center; sf.Trimming = StringTrimming.None; sf.FormatFlags = StringFormatFlags.NoWrap;
                g.DrawString(text, f, b, area, sf);
            }
        }
    }
}
