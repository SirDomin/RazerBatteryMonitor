using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using System.Windows.Automation;
using System.Windows.Forms;

namespace RazerBatteryMonitor
{
    internal sealed class TaskbarStatusForm : Form
    {
        private const int SegmentWidth = 82;
        private const int WindowHeight = 34;
        private const int WsExToolWindow = 0x00000080;
        private const int WsExNoActivate = 0x08000000;
        private const int SwpNoActivate = 0x0010;
        private const int SwpShowWindow = 0x0040;
        private static readonly IntPtr HwndTopmost = new IntPtr(-1);
        private static readonly Color TransparentColor = Color.Fuchsia;

        private readonly Action showDashboard;
        private readonly Timer placementTimer;
        private readonly Font valueFont;
        private DeviceState mouse;
        private DeviceState headset;
        private bool anchorAvailable;

        internal event EventHandler AnchorAvailabilityChanged;

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int x, int y, int width, int height, uint flags);

        internal TaskbarStatusForm(Action showDashboard, ContextMenuStrip menu)
        {
            this.showDashboard = showDashboard;
            FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.Manual;
            TopMost = true;
            AutoScaleMode = AutoScaleMode.None;
            BackColor = TransparentColor;
            TransparencyKey = TransparentColor;
            ContextMenuStrip = menu;
            DoubleBuffered = true;
            Width = SegmentWidth;
            Height = WindowHeight;
            valueFont = new Font("Segoe UI Semibold", 10.5f, FontStyle.Bold, GraphicsUnit.Point);

            MouseClick += delegate(object sender, MouseEventArgs args)
            {
                if (args.Button == MouseButtons.Left) showDashboard();
            };

            placementTimer = new Timer { Interval = 2000 };
            placementTimer.Tick += delegate { RefreshPlacement(); };
            placementTimer.Start();
        }

        protected override bool ShowWithoutActivation { get { return true; } }

        protected override CreateParams CreateParams
        {
            get
            {
                CreateParams parameters = base.CreateParams;
                parameters.ExStyle |= WsExToolWindow | WsExNoActivate;
                return parameters;
            }
        }

        internal bool AnchorAvailable { get { return anchorAvailable; } }

        internal void UpdateStates(DeviceState mouseState, DeviceState headsetState)
        {
            mouse = IsDisplayable(mouseState) ? mouseState : null;
            headset = IsDisplayable(headsetState) ? headsetState : null;
            int count = (mouse == null ? 0 : 1) + (headset == null ? 0 : 1);
            Width = Math.Max(SegmentWidth, count * SegmentWidth);
            Invalidate();
            RefreshPlacement();
        }

        private static bool IsDisplayable(DeviceState state)
        {
            return state != null && state.IsOnline && state.Level.HasValue;
        }

        private void RefreshPlacement()
        {
            Rectangle anchor;
            bool found = TryFindNotificationChevron(out anchor);
            SetAnchorAvailable(found);

            if (!found || (mouse == null && headset == null) || IsFullscreenWindowActive())
            {
                Hide();
                return;
            }

            int left = anchor.Left - Width - 3;
            int top = anchor.Top + Math.Max(0, (anchor.Height - Height) / 2);
            SetWindowPos(Handle, HwndTopmost, left, top, Width, Height, SwpNoActivate | SwpShowWindow);
            if (!Visible) Show();
        }

        private void SetAnchorAvailable(bool value)
        {
            if (anchorAvailable == value) return;
            anchorAvailable = value;
            EventHandler handler = AnchorAvailabilityChanged;
            if (handler != null) handler(this, EventArgs.Empty);
        }

        private static bool TryFindNotificationChevron(out Rectangle rectangle)
        {
            rectangle = Rectangle.Empty;
            try
            {
                PropertyCondition condition = new PropertyCondition(AutomationElement.AutomationIdProperty, "SystemTrayIcon");
                AutomationElementCollection elements = AutomationElement.RootElement.FindAll(TreeScope.Descendants, condition);
                Rectangle primary = Screen.PrimaryScreen.Bounds;
                foreach (AutomationElement element in elements)
                {
                    System.Windows.Rect bounds = element.Current.BoundingRectangle;
                    if (bounds.IsEmpty || bounds.Width < 1 || bounds.Height < 1) continue;
                    Rectangle candidate = Rectangle.Round(new RectangleF((float)bounds.X, (float)bounds.Y, (float)bounds.Width, (float)bounds.Height));
                    if (candidate.Bottom >= primary.Bottom - 4 && primary.IntersectsWith(candidate))
                    {
                        rectangle = candidate;
                        return true;
                    }
                }
            }
            catch (ElementNotAvailableException) { }
            catch (InvalidOperationException) { }
            catch (COMException) { }
            return false;
        }

        private static bool IsFullscreenWindowActive()
        {
            IntPtr foreground = NativeMethods.GetForegroundWindow();
            if (foreground == IntPtr.Zero) return false;
            NativeMethods.Rect window;
            if (!NativeMethods.GetWindowRect(foreground, out window)) return false;
            Rectangle screen = Screen.FromHandle(foreground).Bounds;
            return window.Left <= screen.Left && window.Top <= screen.Top && window.Right >= screen.Right && window.Bottom >= screen.Bottom;
        }

        protected override void OnPaint(PaintEventArgs args)
        {
            base.OnPaint(args);
            args.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            int x = 0;
            if (mouse != null) { DrawSegment(args.Graphics, mouse, DeviceKind.Mouse, x); x += SegmentWidth; }
            if (headset != null) DrawSegment(args.Graphics, headset, DeviceKind.Headset, x);
        }

        private void DrawSegment(Graphics graphics, DeviceState state, DeviceKind kind, int x)
        {
            Color color = StateColor(state);
            using (Pen pen = new Pen(color, 2.1f))
            using (SolidBrush brush = new SolidBrush(color))
            {
                if (kind == DeviceKind.Mouse) DrawMouse(graphics, pen, x + 2, 7);
                else DrawHeadset(graphics, pen, x + 2, 7);

                string text = state.Level.Value.ToString() + "%";
                TextRenderer.DrawText(graphics, text, valueFont, new Rectangle(x + 25, 5, 48, 25), color,
                    TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding | TextFormatFlags.SingleLine);
                if (state.IsCharging) DrawBolt(graphics, brush, x + 70, 8);
            }
        }

        private static Color StateColor(DeviceState state)
        {
            if (state.IsCharging) return Color.FromArgb(75, 255, 35);
            if (state.Level.Value <= 10) return Color.FromArgb(255, 80, 80);
            if (state.Level.Value <= 25) return Color.FromArgb(255, 190, 55);
            return Color.White;
        }

        private static void DrawMouse(Graphics graphics, Pen pen, int x, int y)
        {
            graphics.DrawEllipse(pen, x + 2, y, 16, 22);
            graphics.DrawLine(pen, x + 10, y, x + 10, y + 8);
            graphics.DrawLine(pen, x + 3, y + 10, x + 17, y + 10);
        }

        private static void DrawHeadset(Graphics graphics, Pen pen, int x, int y)
        {
            graphics.DrawArc(pen, x + 1, y, 19, 19, 190, 160);
            graphics.DrawLine(pen, x + 2, y + 12, x + 2, y + 21);
            graphics.DrawLine(pen, x + 19, y + 12, x + 19, y + 21);
            graphics.DrawArc(pen, x, y + 15, 6, 8, 90, 180);
            graphics.DrawArc(pen, x + 16, y + 15, 6, 8, 270, 180);
        }

        private static void DrawBolt(Graphics graphics, Brush brush, int x, int y)
        {
            Point[] points = { new Point(x + 4, y), new Point(x, y + 8), new Point(x + 4, y + 8), new Point(x + 1, y + 16), new Point(x + 10, y + 6), new Point(x + 6, y + 6), new Point(x + 9, y) };
            graphics.FillPolygon(brush, points);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                placementTimer.Dispose();
                valueFont.Dispose();
            }
            base.Dispose(disposing);
        }

        private static class NativeMethods
        {
            [StructLayout(LayoutKind.Sequential)]
            internal struct Rect { internal int Left, Top, Right, Bottom; }

            [DllImport("user32.dll")]
            internal static extern IntPtr GetForegroundWindow();

            [DllImport("user32.dll")]
            [return: MarshalAs(UnmanagedType.Bool)]
            internal static extern bool GetWindowRect(IntPtr hWnd, out Rect rectangle);
        }
    }
}
