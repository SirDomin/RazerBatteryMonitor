using System;
using System.IO;
using System.Text;
using System.Windows.Forms;

namespace RazerBatteryMonitor
{
    internal sealed class AppSettings
    {
        internal string MonitorDeviceName { get; set; }
        internal bool ShowOverFullscreen { get; set; }

        private static string SettingsPath
        {
            get
            {
                return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "RazerBatteryMonitor", "settings.ini");
            }
        }

        internal static AppSettings Load()
        {
            AppSettings settings = new AppSettings
            {
                MonitorDeviceName = Screen.PrimaryScreen == null ? string.Empty : Screen.PrimaryScreen.DeviceName,
                ShowOverFullscreen = false
            };
            try
            {
                if (!File.Exists(SettingsPath)) return settings;
                foreach (string line in File.ReadAllLines(SettingsPath, Encoding.UTF8))
                {
                    int separator = line.IndexOf('=');
                    if (separator <= 0) continue;
                    string key = line.Substring(0, separator).Trim();
                    string value = line.Substring(separator + 1).Trim();
                    if (key.Equals("Monitor", StringComparison.OrdinalIgnoreCase)) settings.MonitorDeviceName = value;
                    if (key.Equals("OverlayInFullscreen", StringComparison.OrdinalIgnoreCase)) settings.ShowOverFullscreen = value.Equals("true", StringComparison.OrdinalIgnoreCase);
                }
            }
            catch
            {
                // Invalid or inaccessible settings must never prevent the monitor from starting.
            }
            return settings;
        }

        internal void Save()
        {
            string directory = Path.GetDirectoryName(SettingsPath);
            Directory.CreateDirectory(directory);
            File.WriteAllText(SettingsPath,
                "Monitor=" + (MonitorDeviceName ?? string.Empty) + Environment.NewLine +
                "OverlayInFullscreen=" + (ShowOverFullscreen ? "true" : "false") + Environment.NewLine,
                Encoding.UTF8);
        }
    }
}
