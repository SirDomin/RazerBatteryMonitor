using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing.Text;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows.Automation;
using System.Windows.Forms;

namespace RazerBatteryMonitor
{
    internal sealed class TaskbarStatusForm : Form
    {
        private const int SegmentWidth = 96;
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
        private readonly Bitmap mouseAsset;
        private readonly Bitmap headsetAsset;
        private DeviceState mouse;
        private DeviceState headset;
        private bool anchorAvailable;
        private Rectangle lastAnchor;
        private int anchorMisses;
        private IntPtr taskbarOwner;

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
            mouseAsset = LoadMonochromeMask("RazerBatteryMonitor.Assets.mouse.png");
            headsetAsset = LoadMonochromeMask("RazerBatteryMonitor.Assets.headset.png");

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
            if (found)
            {
                lastAnchor = anchor;
                anchorMisses = 0;
            }
            else if (!lastAnchor.IsEmpty && ++anchorMisses < 3)
            {
                anchor = lastAnchor;
                found = true;
            }
            else
            {
                lastAnchor = Rectangle.Empty;
            }
            SetAnchorAvailable(found);

            if (!found || (mouse == null && headset == null) || ShouldHideForUserState())
            {
                Hide();
                return;
            }

            int left = anchor.Left - Width - 3;
            int top = anchor.Top + Math.Max(0, (anchor.Height - Height) / 2);
            if (!Visible) Show();
            EnsureTaskbarOwner();
            SetWindowPos(Handle, HwndTopmost, left, top, Width, Height, SwpNoActivate | SwpShowWindow);
        }

        private void EnsureTaskbarOwner()
        {
            IntPtr taskbar = NativeMethods.FindWindow("Shell_TrayWnd", null);
            if (taskbar == IntPtr.Zero || taskbar == taskbarOwner) return;
            NativeMethods.SetWindowLongPtr(Handle, NativeMethods.GwlpHwndParent, taskbar);
            taskbarOwner = taskbar;
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

        private static bool ShouldHideForUserState()
        {
            NativeMethods.QueryUserNotificationState state;
            int result = NativeMethods.SHQueryUserNotificationState(out state);
            if (result != 0) return false;
            return state == NativeMethods.QueryUserNotificationState.NotPresent
                || state == NativeMethods.QueryUserNotificationState.Busy
                || state == NativeMethods.QueryUserNotificationState.RunningD3dFullScreen
                || state == NativeMethods.QueryUserNotificationState.PresentationMode;
        }

        protected override void OnPaint(PaintEventArgs args)
        {
            base.OnPaint(args);
            args.Graphics.SmoothingMode = SmoothingMode.None;
            args.Graphics.TextRenderingHint = TextRenderingHint.SingleBitPerPixelGridFit;
            int x = 0;
            if (mouse != null) { DrawSegment(args.Graphics, mouse, DeviceKind.Mouse, x); x += SegmentWidth; }
            if (headset != null) DrawSegment(args.Graphics, headset, DeviceKind.Headset, x);
        }

        private void DrawSegment(Graphics graphics, DeviceState state, DeviceKind kind, int x)
        {
            Color color = StateColor(state);
            using (SolidBrush brush = new SolidBrush(color))
            {
                Bitmap asset = kind == DeviceKind.Mouse ? mouseAsset : headsetAsset;
                DrawTintedMask(graphics, asset, new Rectangle(x, 5, 24, 24), color);
                if (kind == DeviceKind.Mouse)
                    graphics.FillRectangle(brush, x + 11, 9, 2, 6);

                string text = state.Level.Value.ToString() + "%";
                using (StringFormat format = (StringFormat)StringFormat.GenericTypographic.Clone())
                {
                    format.FormatFlags |= StringFormatFlags.NoWrap;
                    graphics.DrawString(text, valueFont, brush, new PointF(x + 31, 7), format);
                }
                if (state.IsCharging) DrawBolt(graphics, brush, x + 84, 8);
            }
        }

        private static Bitmap LoadMonochromeMask(string resourceName)
        {
            using (Stream stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(resourceName))
            {
                if (stream == null) return null;
                using (Bitmap source = new Bitmap(stream))
                using (Bitmap resized = new Bitmap(24, 24, PixelFormat.Format32bppArgb))
                {
                    using (Graphics graphics = Graphics.FromImage(resized))
                    {
                        graphics.Clear(Color.Transparent);
                        graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
                        graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
                        graphics.DrawImage(source, new Rectangle(0, 0, 24, 24));
                    }

                    bool[,] solid = new bool[24, 24];
                    for (int y = 0; y < 24; y++)
                    {
                        for (int x = 0; x < 24; x++)
                            solid[x, y] = resized.GetPixel(x, y).A >= 96;
                    }

                    Bitmap mask = new Bitmap(24, 24, PixelFormat.Format32bppArgb);
                    for (int y = 0; y < 24; y++)
                    {
                        for (int x = 0; x < 24; x++)
                        {
                            if (!solid[x, y]) continue;
                            bool edge = false;
                            for (int offsetY = -1; offsetY <= 1 && !edge; offsetY++)
                            {
                                for (int offsetX = -1; offsetX <= 1; offsetX++)
                                {
                                    int sampleX = x + offsetX;
                                    int sampleY = y + offsetY;
                                    if (sampleX < 0 || sampleY < 0 || sampleX >= 24 || sampleY >= 24 || !solid[sampleX, sampleY])
                                    {
                                        edge = true;
                                        break;
                                    }
                                }
                            }
                            if (edge) mask.SetPixel(x, y, Color.White);
                        }
                    }
                    return mask;
                }
            }
        }

        private static void DrawTintedMask(Graphics graphics, Bitmap mask, Rectangle destination, Color color)
        {
            if (mask == null) return;
            ColorMatrix matrix = new ColorMatrix(new float[][]
            {
                new float[] { 0, 0, 0, 0, 0 },
                new float[] { 0, 0, 0, 0, 0 },
                new float[] { 0, 0, 0, 0, 0 },
                new float[] { 0, 0, 0, 1, 0 },
                new float[] { color.R / 255f, color.G / 255f, color.B / 255f, 0, 1 }
            });
            using (ImageAttributes attributes = new ImageAttributes())
            {
                attributes.SetColorMatrix(matrix);
                InterpolationMode previous = graphics.InterpolationMode;
                graphics.InterpolationMode = InterpolationMode.NearestNeighbor;
                graphics.DrawImage(mask, destination, 0, 0, mask.Width, mask.Height, GraphicsUnit.Pixel, attributes);
                graphics.InterpolationMode = previous;
            }
        }

        private static Color StateColor(DeviceState state)
        {
            if (state.IsCharging) return Color.FromArgb(75, 255, 35);
            if (state.Level.Value <= 10) return Color.FromArgb(255, 80, 80);
            if (state.Level.Value <= 25) return Color.FromArgb(255, 190, 55);
            return Color.White;
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
                if (mouseAsset != null) mouseAsset.Dispose();
                if (headsetAsset != null) headsetAsset.Dispose();
            }
            base.Dispose(disposing);
        }

        private static class NativeMethods
        {
            internal const int GwlpHwndParent = -8;

            internal enum QueryUserNotificationState
            {
                NotPresent = 1,
                Busy = 2,
                RunningD3dFullScreen = 3,
                PresentationMode = 4,
                AcceptsNotifications = 5,
                QuietTime = 6,
                App = 7
            }

            [DllImport("shell32.dll")]
            internal static extern int SHQueryUserNotificationState(out QueryUserNotificationState state);

            [DllImport("user32.dll", CharSet = CharSet.Unicode)]
            internal static extern IntPtr FindWindow(string className, string windowName);

            [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW", SetLastError = true)]
            internal static extern IntPtr SetWindowLongPtr(IntPtr window, int index, IntPtr newValue);
        }
    }
}
