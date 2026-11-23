using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing.Text;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;

namespace RazerBatteryMonitor
{
    internal sealed class TaskbarStatusForm : Form
    {
        private const int LogicalSegmentWidth = 76;
        private const int LogicalWindowHeight = 24;
        private const int LogicalIconSize = 20;
        private const float StandardTaskbarHeight = 48f;
        private const int WsExToolWindow = 0x00000080;
        private const int WsExLayered = 0x00080000;
        private const int WsExNoActivate = 0x08000000;
        private const int SwpNoActivate = 0x0010;
        private const int SwpShowWindow = 0x0040;
        private const int UlwAlpha = 0x00000002;
        private const byte AcSrcOver = 0x00;
        private const byte AcSrcAlpha = 0x01;
        private static readonly IntPtr HwndTopmost = new IntPtr(-1);

        private readonly Action showDashboard;
        private readonly Timer placementTimer;
        private readonly Timer zOrderTimer;
        private readonly Font valueFont;
        private readonly Bitmap mouseAsset;
        private readonly Bitmap headsetAsset;
        private readonly Bitmap steelSeriesAsset;
        private readonly Bitmap deathAdderAsset;
        private IList<DeviceState> devices = new List<DeviceState>();
        private Rectangle lastAnchor;
        private string targetMonitorDeviceName;
        private bool showOverFullscreen;
        private float displayScale = 1f;
        private int deviceCount;

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int x, int y, int width, int height, uint flags);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool UpdateLayeredWindow(IntPtr hWnd, IntPtr destinationDc, ref NativePoint destination, ref NativeSize size, IntPtr sourceDc, ref NativePoint source, int colorKey, ref BlendFunction blend, int flags);

        [DllImport("user32.dll")]
        private static extern IntPtr GetDC(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern int ReleaseDC(IntPtr hWnd, IntPtr dc);

        [DllImport("gdi32.dll")]
        private static extern IntPtr CreateCompatibleDC(IntPtr dc);

        [DllImport("gdi32.dll")]
        private static extern bool DeleteDC(IntPtr dc);

        [DllImport("gdi32.dll")]
        private static extern IntPtr SelectObject(IntPtr dc, IntPtr objectHandle);

        [DllImport("gdi32.dll")]
        private static extern bool DeleteObject(IntPtr objectHandle);

        [StructLayout(LayoutKind.Sequential)]
        private struct NativePoint
        {
            internal int X;
            internal int Y;
            internal NativePoint(int x, int y) { X = x; Y = y; }
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct NativeSize
        {
            internal int Width;
            internal int Height;
            internal NativeSize(int width, int height) { Width = width; Height = height; }
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        private struct BlendFunction
        {
            internal byte BlendOp;
            internal byte BlendFlags;
            internal byte SourceConstantAlpha;
            internal byte AlphaFormat;
        }

        internal TaskbarStatusForm(Action showDashboard, ContextMenuStrip menu)
        {
            this.showDashboard = showDashboard;
            FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.Manual;
            TopMost = true;
            AutoScaleMode = AutoScaleMode.None;
            ContextMenuStrip = menu;
            DoubleBuffered = true;
            Width = LogicalSegmentWidth;
            Height = LogicalWindowHeight;
            valueFont = new Font("Segoe UI Semibold", 10.5f, FontStyle.Bold, GraphicsUnit.Point);
            mouseAsset = LoadCleanIcon("RazerBatteryMonitor.Assets.mouse.png", 48);
            headsetAsset = LoadCleanIcon("RazerBatteryMonitor.Assets.headset.png", 180);
            steelSeriesAsset = LoadCleanIcon("RazerBatteryMonitor.Assets.steelseries.png", 180);
            deathAdderAsset = LoadCleanIcon("RazerBatteryMonitor.Assets.deathadder.png", 200);

            MouseClick += delegate(object sender, MouseEventArgs args)
            {
                if (args.Button == MouseButtons.Left) showDashboard();
            };

            placementTimer = new Timer { Interval = 2000 };
            placementTimer.Tick += delegate { RefreshPlacement(); };
            placementTimer.Start();
            zOrderTimer = new Timer { Interval = 100 };
            zOrderTimer.Tick += delegate { KeepAboveTaskbar(); };
            zOrderTimer.Start();
        }

        protected override bool ShowWithoutActivation { get { return true; } }

        protected override CreateParams CreateParams
        {
            get
            {
                CreateParams parameters = base.CreateParams;
                parameters.ExStyle |= WsExToolWindow | WsExLayered | WsExNoActivate;
                return parameters;
            }
        }

        internal void UpdateStates(IList<DeviceState> states)
        {
            devices = states.Where(IsDisplayable).OrderBy(DeviceOrder).ToList();
            deviceCount = devices.Count;
            ApplyDimensions();
            RefreshPlacement();
        }

        internal void Configure(string monitorDeviceName, bool allowOverFullscreen)
        {
            targetMonitorDeviceName = monitorDeviceName;
            showOverFullscreen = allowOverFullscreen;
            lastAnchor = Rectangle.Empty;
            RefreshPlacement();
        }

        private static bool IsDisplayable(DeviceState state)
        {
            return state != null && state.IsOnline && state.Level.HasValue;
        }

        private static int DeviceOrder(DeviceState state)
        {
            if (state.Name.IndexOf("Naga", StringComparison.OrdinalIgnoreCase) >= 0) return 0;
            if (state.Name.IndexOf("Kraken", StringComparison.OrdinalIgnoreCase) >= 0) return 1;
            if (state.Name.IndexOf("SteelSeries", StringComparison.OrdinalIgnoreCase) >= 0 || state.Name.IndexOf("Arctis", StringComparison.OrdinalIgnoreCase) >= 0) return 2;
            if (state.Name.IndexOf("DeathAdder", StringComparison.OrdinalIgnoreCase) >= 0) return 3;
            return state.Kind == DeviceKind.Mouse ? 4 : 5;
        }

        private void RefreshPlacement()
        {
            Rectangle anchor;
            bool found = TryFindTaskbarAnchor(out anchor);
            if (found)
            {
                lastAnchor = anchor;
            }
            if (found) UpdateDisplayScale(anchor);

            if (!found || devices.Count == 0 || IsTaskbarCovered(anchor))
            {
                Hide();
                return;
            }

            int left = anchor.Left - Width - 3;
            int top = anchor.Top + Math.Max(0, (anchor.Height - Height) / 2);
            if (!Visible) Show();
            SetWindowPos(Handle, HwndTopmost, left, top, Width, Height, SwpNoActivate | SwpShowWindow);
            RenderLayeredWindow();
        }

        private void KeepAboveTaskbar()
        {
            if (lastAnchor.IsEmpty || devices.Count == 0) return;
            if (IsTaskbarCovered(lastAnchor))
            {
                if (Visible) Hide();
                return;
            }
            if (!Visible) return;
            int left = lastAnchor.Left - Width - 3;
            int top = lastAnchor.Top + Math.Max(0, (lastAnchor.Height - Height) / 2);
            SetWindowPos(Handle, HwndTopmost, left, top, Width, Height, SwpNoActivate | SwpShowWindow);
        }

        private void UpdateDisplayScale(Rectangle anchor)
        {
            float scale = Math.Max(0.75f, Math.Min(3f, anchor.Height / StandardTaskbarHeight));
            if (Math.Abs(scale - displayScale) < 0.01f) return;
            displayScale = scale;
            ApplyDimensions();
        }

        private void ApplyDimensions()
        {
            int segmentWidth = ScaleLogical(LogicalSegmentWidth);
            Width = Math.Max(segmentWidth, deviceCount * segmentWidth);
            Height = ScaleLogical(LogicalWindowHeight);
        }

        private int ScaleLogical(int value)
        {
            return Math.Max(1, (int)Math.Round(value * displayScale));
        }

        private bool TryFindTaskbarAnchor(out Rectangle rectangle)
        {
            rectangle = Rectangle.Empty;
            Screen target = Screen.AllScreens.FirstOrDefault(delegate(Screen screen)
            {
                return string.Equals(screen.DeviceName, targetMonitorDeviceName, StringComparison.OrdinalIgnoreCase);
            }) ?? Screen.PrimaryScreen;
            if (target == null) return false;

            IntPtr taskbar = IntPtr.Zero;
            NativeMethods.EnumWindows(delegate(IntPtr window, IntPtr parameter)
            {
                StringBuilder className = new StringBuilder(64);
                NativeMethods.GetClassName(window, className, className.Capacity);
                string value = className.ToString();
                if (value != "Shell_TrayWnd" && value != "Shell_SecondaryTrayWnd") return true;
                NativeMethods.Rect candidateBounds;
                if (!NativeMethods.GetWindowRect(window, out candidateBounds)) return true;
                Rectangle candidate = Rectangle.FromLTRB(candidateBounds.Left, candidateBounds.Top, candidateBounds.Right, candidateBounds.Bottom);
                if (!candidate.IntersectsWith(target.Bounds)) return true;
                taskbar = window;
                return false;
            }, IntPtr.Zero);
            if (taskbar == IntPtr.Zero) return false;

            NativeMethods.Rect taskbarBounds;
            if (!NativeMethods.GetWindowRect(taskbar, out taskbarBounds)) return false;
            IntPtr notificationArea = NativeMethods.FindWindowEx(taskbar, IntPtr.Zero, "TrayNotifyWnd", null);
            if (notificationArea != IntPtr.Zero)
            {
                NativeMethods.Rect bounds;
                if (NativeMethods.GetWindowRect(notificationArea, out bounds) && bounds.Right > bounds.Left && bounds.Bottom > bounds.Top)
                {
                    rectangle = Rectangle.FromLTRB(bounds.Left, bounds.Top, bounds.Right, bounds.Bottom);
                    return true;
                }
            }

            if (taskbarBounds.Right <= taskbarBounds.Left || taskbarBounds.Bottom <= taskbarBounds.Top) return false;
            int taskbarHeight = taskbarBounds.Bottom - taskbarBounds.Top;
            int reservedForClock = Math.Max(180, (int)Math.Round(180.0 * taskbarHeight / StandardTaskbarHeight));
            int anchorRight = taskbarBounds.Right - reservedForClock;
            rectangle = Rectangle.FromLTRB(anchorRight - 12, taskbarBounds.Top, anchorRight, taskbarBounds.Bottom);
            return true;
        }

        private bool IsTaskbarCovered(Rectangle anchor)
        {
            if (showOverFullscreen) return false;
            IntPtr foreground = NativeMethods.GetForegroundWindow();
            if (foreground == IntPtr.Zero) return false;
            IntPtr taskbar = NativeMethods.FindWindow("Shell_TrayWnd", null);
            if (foreground == taskbar) return false;

            StringBuilder className = new StringBuilder(64);
            NativeMethods.GetClassName(foreground, className, className.Capacity);
            string value = className.ToString();
            if (value == "Progman" || value == "WorkerW" || value == "Shell_TrayWnd") return false;

            NativeMethods.Rect bounds;
            if (!NativeMethods.GetWindowRect(foreground, out bounds)) return false;
            int sampleX = anchor.Left + anchor.Width / 2;
            int sampleY = anchor.Top + anchor.Height / 2;
            return sampleX >= bounds.Left && sampleX < bounds.Right && sampleY >= bounds.Top && sampleY < bounds.Bottom;
        }

        protected override void OnPaint(PaintEventArgs args)
        {
        }

        private void DrawContents(Graphics graphics)
        {
            graphics.Clear(Color.Transparent);
            graphics.SmoothingMode = SmoothingMode.HighQuality;
            graphics.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
            int x = 0;
            foreach (DeviceState state in devices)
            {
                DrawSegment(graphics, state, x);
                x += ScaleLogical(LogicalSegmentWidth);
            }
        }

        private void RenderLayeredWindow()
        {
            if (!IsHandleCreated || Width <= 0 || Height <= 0) return;
            using (Bitmap surface = new Bitmap(Width, Height, PixelFormat.Format32bppArgb))
            {
                using (Graphics graphics = Graphics.FromImage(surface)) DrawContents(graphics);
                IntPtr screenDc = GetDC(IntPtr.Zero);
                IntPtr memoryDc = CreateCompatibleDC(screenDc);
                IntPtr bitmapHandle = IntPtr.Zero;
                IntPtr previousBitmap = IntPtr.Zero;
                try
                {
                    bitmapHandle = surface.GetHbitmap(Color.FromArgb(0));
                    previousBitmap = SelectObject(memoryDc, bitmapHandle);
                    NativePoint destination = new NativePoint(Left, Top);
                    NativePoint source = new NativePoint(0, 0);
                    NativeSize size = new NativeSize(Width, Height);
                    BlendFunction blend = new BlendFunction
                    {
                        BlendOp = AcSrcOver,
                        BlendFlags = 0,
                        SourceConstantAlpha = 255,
                        AlphaFormat = AcSrcAlpha
                    };
                    UpdateLayeredWindow(Handle, screenDc, ref destination, ref size, memoryDc, ref source, 0, ref blend, UlwAlpha);
                }
                finally
                {
                    if (previousBitmap != IntPtr.Zero) SelectObject(memoryDc, previousBitmap);
                    if (bitmapHandle != IntPtr.Zero) DeleteObject(bitmapHandle);
                    DeleteDC(memoryDc);
                    ReleaseDC(IntPtr.Zero, screenDc);
                }
            }
        }

        private void DrawSegment(Graphics graphics, DeviceState state, int x)
        {
            Color color = StateColor(state);
            using (SolidBrush brush = new SolidBrush(color))
            {
                Rectangle iconBounds = new Rectangle(x, ScaleLogical(2), ScaleLogical(LogicalIconSize), ScaleLogical(LogicalIconSize));
                DrawOriginalIcon(graphics, AssetFor(state), iconBounds);

                string text = state.Level.Value.ToString() + "%";
                using (StringFormat format = (StringFormat)StringFormat.GenericTypographic.Clone())
                {
                    format.FormatFlags |= StringFormatFlags.NoWrap;
                    graphics.DrawString(text, valueFont, brush, new PointF(x + ScaleLogical(25), ScaleLogical(5)), format);
                }
                if (state.IsCharging) DrawBolt(graphics, brush, x + ScaleLogical(63), ScaleLogical(5));
            }
        }

        private Bitmap AssetFor(DeviceState state)
        {
            if (state.Name.IndexOf("DeathAdder", StringComparison.OrdinalIgnoreCase) >= 0) return deathAdderAsset;
            if (state.Name.IndexOf("SteelSeries", StringComparison.OrdinalIgnoreCase) >= 0 || state.Name.IndexOf("Arctis", StringComparison.OrdinalIgnoreCase) >= 0) return steelSeriesAsset;
            if (state.Kind == DeviceKind.Headset) return headsetAsset;
            return mouseAsset;
        }

        private static Bitmap LoadCleanIcon(string resourceName, int brightnessThreshold)
        {
            using (Stream stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(resourceName))
            {
                if (stream == null) return null;
                using (Bitmap source = new Bitmap(stream))
                using (Bitmap resized = new Bitmap(64, 64, PixelFormat.Format32bppArgb))
                {
                    using (Graphics graphics = Graphics.FromImage(resized))
                    {
                        graphics.Clear(Color.Transparent);
                        graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
                        graphics.CompositingQuality = CompositingQuality.HighQuality;
                        graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
                        graphics.DrawImage(source, new Rectangle(1, 1, 62, 62));
                    }

                    Bitmap cleaned = new Bitmap(64, 64, PixelFormat.Format32bppArgb);
                    for (int y = 0; y < cleaned.Height; y++)
                    {
                        for (int x = 0; x < cleaned.Width; x++)
                        {
                            Color pixel = resized.GetPixel(x, y);
                            int brightness = Math.Max(pixel.R, Math.Max(pixel.G, pixel.B));
                            if (pixel.A >= 24 && brightness >= brightnessThreshold) cleaned.SetPixel(x, y, Color.White);
                        }
                    }
                    return cleaned;
                }
            }
        }

        private static void DrawOriginalIcon(Graphics graphics, Bitmap image, Rectangle destination)
        {
            if (image == null) return;
            InterpolationMode interpolation = graphics.InterpolationMode;
            CompositingQuality compositing = graphics.CompositingQuality;
            PixelOffsetMode pixelOffset = graphics.PixelOffsetMode;
            graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
            graphics.CompositingQuality = CompositingQuality.HighQuality;
            graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
            graphics.DrawImage(image, destination);
            graphics.DrawImage(image, destination);
            graphics.InterpolationMode = interpolation;
            graphics.CompositingQuality = compositing;
            graphics.PixelOffsetMode = pixelOffset;
        }

        private static Color StateColor(DeviceState state)
        {
            if (state.IsCharging) return Color.FromArgb(75, 255, 35);
            if (state.Level.Value <= 10) return Color.FromArgb(255, 80, 80);
            if (state.Level.Value <= 25) return Color.FromArgb(255, 190, 55);
            return Color.White;
        }

        private void DrawBolt(Graphics graphics, Brush brush, int x, int y)
        {
            Point[] points = {
                new Point(x + ScaleLogical(3), y),
                new Point(x, y + ScaleLogical(6)),
                new Point(x + ScaleLogical(3), y + ScaleLogical(6)),
                new Point(x + ScaleLogical(1), y + ScaleLogical(11)),
                new Point(x + ScaleLogical(7), y + ScaleLogical(4)),
                new Point(x + ScaleLogical(4), y + ScaleLogical(4)),
                new Point(x + ScaleLogical(6), y)
            };
            graphics.FillPolygon(brush, points);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                placementTimer.Dispose();
                zOrderTimer.Dispose();
                valueFont.Dispose();
                if (mouseAsset != null) mouseAsset.Dispose();
                if (headsetAsset != null) headsetAsset.Dispose();
                if (steelSeriesAsset != null) steelSeriesAsset.Dispose();
                if (deathAdderAsset != null) deathAdderAsset.Dispose();
            }
            base.Dispose(disposing);
        }

        private static class NativeMethods
        {
            internal delegate bool EnumWindowsProc(IntPtr window, IntPtr parameter);

            [StructLayout(LayoutKind.Sequential)]
            internal struct Rect { internal int Left, Top, Right, Bottom; }

            [DllImport("user32.dll", CharSet = CharSet.Unicode)]
            internal static extern IntPtr FindWindow(string className, string windowName);

            [DllImport("user32.dll", CharSet = CharSet.Unicode)]
            internal static extern IntPtr FindWindowEx(IntPtr parent, IntPtr childAfter, string className, string windowName);

            [DllImport("user32.dll")]
            [return: MarshalAs(UnmanagedType.Bool)]
            internal static extern bool EnumWindows(EnumWindowsProc callback, IntPtr parameter);

            [DllImport("user32.dll")]
            [return: MarshalAs(UnmanagedType.Bool)]
            internal static extern bool GetWindowRect(IntPtr window, out Rect rectangle);

            [DllImport("user32.dll")]
            internal static extern IntPtr GetForegroundWindow();

            [DllImport("user32.dll", CharSet = CharSet.Unicode)]
            internal static extern int GetClassName(IntPtr window, StringBuilder className, int maximumCount);
        }

    }
}
