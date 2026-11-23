using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Threading;
using System.Windows.Forms;

namespace RazerBatteryMonitor
{
    internal sealed class TrayApplicationContext : ApplicationContext
    {
        private readonly NotifyIcon mouseIcon;
        private readonly NotifyIcon headsetIcon;
        private readonly System.Windows.Forms.Timer timer;
        private readonly MonitorService monitor;
        private readonly DashboardForm dashboard;
        private IList<DeviceState> states;
        private int refreshing;

        internal TrayApplicationContext(bool showDashboard)
        {
            monitor = new MonitorService();
            states = new List<DeviceState>();
            dashboard = new DashboardForm();
            if (dashboard.Handle == IntPtr.Zero) throw new InvalidOperationException("The dashboard window could not be initialized.");
            dashboard.FormClosing += delegate(object sender, FormClosingEventArgs args) { args.Cancel = true; dashboard.Hide(); };

            ContextMenuStrip menu = new ContextMenuStrip();
            menu.Items.Add("Pokaż urządzenia", null, delegate { ShowDashboard(); });
            menu.Items.Add("Odśwież teraz", null, delegate { RefreshAsync(); });
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add("Zakończ", null, delegate { Exit(); });

            mouseIcon = CreateIcon(DeviceKind.Mouse, "Mysz Razer · oczekiwanie na odczyt", menu);
            headsetIcon = CreateIcon(DeviceKind.Headset, "Słuchawki Razer · oczekiwanie na odczyt", menu);
            timer = new System.Windows.Forms.Timer { Interval = 15000 };
            timer.Tick += delegate { RefreshAsync(); };
            timer.Start();
            RefreshAsync();
            if (showDashboard) ShowDashboard();
        }

        private NotifyIcon CreateIcon(DeviceKind kind, string text, ContextMenuStrip menu)
        {
            NotifyIcon icon = new NotifyIcon();
            icon.Icon = TrayIconRenderer.Render(kind, null, false, false);
            icon.Text = text;
            icon.ContextMenuStrip = menu;
            icon.Visible = true;
            icon.MouseClick += delegate(object sender, MouseEventArgs args) { if (args.Button == MouseButtons.Left) ShowDashboard(); };
            return icon;
        }

        private void RefreshAsync()
        {
            if (Interlocked.Exchange(ref refreshing, 1) != 0) return;
            ThreadPool.QueueUserWorkItem(delegate
            {
                IList<DeviceState> read;
                try { read = monitor.ReadAll(); }
                catch (Exception ex)
                {
                    read = new List<DeviceState> { new DeviceState { Name = "Błąd odczytu", Error = ex.Message, IsOnline = false } };
                }
                dashboard.BeginInvoke(new Action(delegate
                {
                    states = read;
                    UpdateIcons();
                    if (dashboard.Visible) dashboard.UpdateStates(states);
                    Interlocked.Exchange(ref refreshing, 0);
                }));
            });
        }

        private void UpdateIcons()
        {
            DeviceState mouse = states.Where(delegate(DeviceState s) { return s.Kind == DeviceKind.Mouse && s.IsOnline && s.Level.HasValue; }).OrderBy(delegate(DeviceState s) { return s.Level.Value; }).FirstOrDefault();
            if (mouse == null) mouse = states.FirstOrDefault(delegate(DeviceState s) { return s.Kind == DeviceKind.Mouse; });
            DeviceState headset = states.FirstOrDefault(delegate(DeviceState s) { return s.Kind == DeviceKind.Headset; });
            SetIcon(mouseIcon, DeviceKind.Mouse, mouse, MouseTooltip());
            SetIcon(headsetIcon, DeviceKind.Headset, headset, headset == null ? "Słuchawki Razer · brak danych" : Tooltip(headset));
        }

        private string MouseTooltip()
        {
            IEnumerable<DeviceState> mice = states.Where(delegate(DeviceState s) { return s.Kind == DeviceKind.Mouse; });
            string value = string.Join(" | ", mice.Select(delegate(DeviceState s) { return s.Name.Replace("Razer ", string.Empty) + " " + s.ShortStatus(); }).ToArray());
            return TrimTooltip(string.IsNullOrEmpty(value) ? "Mysz Razer · brak danych" : value);
        }

        private static string Tooltip(DeviceState state)
        {
            return TrimTooltip(string.Format("{0} · {1}", state.Name, state.ShortStatus()));
        }

        private static string TrimTooltip(string value)
        {
            return value.Length <= 63 ? value : value.Substring(0, 60) + "...";
        }

        private static void SetIcon(NotifyIcon notify, DeviceKind kind, DeviceState state, string tooltip)
        {
            Icon previous = notify.Icon;
            notify.Icon = TrayIconRenderer.Render(kind, state == null ? (int?)null : state.Level, state != null && state.IsCharging, state != null && state.IsOnline);
            notify.Text = tooltip;
            if (previous != null) previous.Dispose();
        }

        private void ShowDashboard()
        {
            dashboard.UpdateStates(states);
            dashboard.Show();
            dashboard.WindowState = FormWindowState.Normal;
            dashboard.Activate();
        }

        private void Exit()
        {
            timer.Stop();
            mouseIcon.Visible = false;
            headsetIcon.Visible = false;
            mouseIcon.Dispose();
            headsetIcon.Dispose();
            dashboard.Dispose();
            ExitThread();
        }
    }
}

