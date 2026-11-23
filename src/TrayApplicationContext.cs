using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Windows.Forms;

namespace RazerBatteryMonitor
{
    internal sealed class TrayApplicationContext : ApplicationContext
    {
        private readonly System.Windows.Forms.Timer timer;
        private readonly MonitorService monitor;
        private readonly DashboardForm dashboard;
        private readonly TaskbarStatusForm taskbarStatus;
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

            taskbarStatus = new TaskbarStatusForm(ShowDashboard, menu);
            timer = new System.Windows.Forms.Timer { Interval = 15000 };
            timer.Tick += delegate { RefreshAsync(); };
            timer.Start();
            RefreshAsync();
            if (showDashboard) ShowDashboard();
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
            taskbarStatus.UpdateStates(states);
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
            taskbarStatus.Dispose();
            dashboard.Dispose();
            ExitThread();
        }
    }
}

