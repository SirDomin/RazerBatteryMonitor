using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;

namespace RazerBatteryMonitor
{
    internal static class TrayIconRenderer
    {
        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool DestroyIcon(IntPtr handle);

        internal static Icon Render(DeviceKind kind, int? level, bool charging, bool online)
        {
            using (Bitmap bitmap = new Bitmap(32, 32))
            using (Graphics graphics = Graphics.FromImage(bitmap))
            {
                graphics.SmoothingMode = SmoothingMode.AntiAlias;
                graphics.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;
                Color color = ChooseColor(level, charging, online);
                using (Pen pen = new Pen(color, 2.4f))
                using (SolidBrush brush = new SolidBrush(color))
                {
                    if (kind == DeviceKind.Mouse) DrawMouse(graphics, pen);
                    else DrawHeadset(graphics, pen);
                    string text = online && level.HasValue ? level.Value.ToString() : "--";
                    using (Font font = new Font("Segoe UI", text.Length >= 3 ? 9.0f : 10.0f, FontStyle.Bold, GraphicsUnit.Pixel))
                    {
                        SizeF size = graphics.MeasureString(text, font);
                        graphics.DrawString(text, font, brush, 31 - size.Width, 18);
                    }
                    if (charging) DrawBolt(graphics, brush);
                }
                IntPtr handle = bitmap.GetHicon();
                try { return (Icon)Icon.FromHandle(handle).Clone(); }
                finally { DestroyIcon(handle); }
            }
        }

        private static Color ChooseColor(int? level, bool charging, bool online)
        {
            if (!online || !level.HasValue) return Color.FromArgb(170, 170, 170);
            if (charging) return Color.FromArgb(57, 255, 20);
            if (level.Value <= 10) return Color.FromArgb(255, 70, 70);
            if (level.Value <= 25) return Color.FromArgb(255, 177, 46);
            return Color.White;
        }

        private static void DrawMouse(Graphics g, Pen pen)
        {
            RectangleF body = new RectangleF(3, 2, 12, 17);
            g.DrawEllipse(pen, body);
            g.DrawLine(pen, 9, 2, 9, 8);
            g.DrawLine(pen, 4, 9, 14, 9);
        }

        private static void DrawHeadset(Graphics g, Pen pen)
        {
            g.DrawArc(pen, 2, 2, 15, 15, 190, 160);
            g.DrawLine(pen, 3, 10, 3, 17);
            g.DrawLine(pen, 16, 10, 16, 17);
            g.DrawArc(pen, 1, 12, 5, 7, 90, 180);
            g.DrawArc(pen, 13, 12, 5, 7, 270, 180);
        }

        private static void DrawBolt(Graphics g, Brush brush)
        {
            PointF[] points = { new PointF(22, 1), new PointF(18, 9), new PointF(22, 9), new PointF(19, 16), new PointF(28, 7), new PointF(24, 7), new PointF(27, 1) };
            g.FillPolygon(brush, points);
        }
    }
}

