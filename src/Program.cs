using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Windows.Forms;

namespace RazerBatteryMonitor
{
    internal static class Program
    {
        [STAThread]
        private static int Main(string[] args)
        {
            if (args.Length >= 2 && string.Equals(args[0], "--probe-once", StringComparison.OrdinalIgnoreCase))
                return ProbeOnce(args[1]);

            bool created;
            using (Mutex mutex = new Mutex(true, "Local\\RazerBatteryMonitor-SirDomin", out created))
            {
                if (!created) return 0;
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                bool showDashboard = Array.Exists(args, delegate(string arg) { return string.Equals(arg, "--show", StringComparison.OrdinalIgnoreCase); });
                Application.Run(new TrayApplicationContext(showDashboard));
            }
            return 0;
        }

        private static int ProbeOnce(string outputPath)
        {
            try
            {
                IList<DeviceState> states = new MonitorService().ReadAll();
                StringBuilder text = new StringBuilder();
                text.AppendLine("Razer Battery Monitor diagnostics");
                text.AppendLine(string.Format("Time: {0:O}", DateTime.Now));
                text.AppendLine("Administrator: not requested (asInvoker)");
                text.AppendLine();
                foreach (DeviceState state in states)
                {
                    text.AppendLine(string.Format("{0}: online={1}, level={2}, charging={3}, source={4}, updated={5:O}, error={6}",
                        state.Name, state.IsOnline, state.Level.HasValue ? state.Level.Value.ToString() : "n/a", state.IsCharging, state.Source, state.UpdatedAt, state.Error));
                }
                File.WriteAllText(Path.GetFullPath(outputPath), text.ToString(), Encoding.UTF8);
                return 0;
            }
            catch (Exception ex)
            {
                File.WriteAllText(Path.GetFullPath(outputPath), ex.ToString(), Encoding.UTF8);
                return 1;
            }
        }
    }
}

