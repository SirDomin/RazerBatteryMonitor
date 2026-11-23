using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;

namespace RazerBatteryMonitor
{
    internal static class SynapseLogReader
    {
        private const int TailBytes = 2 * 1024 * 1024;
        private static readonly Regex BatteryEvent = new Regex(
            @"\[(?<time>\d{4}/\d{2}/\d{2} \d{2}:\d{2}:\d{2}\.\d+)\][^\r\n]*GET_BATTERY_STATE[^\r\n]*?\{""chargingStatus"":""(?<status>[^""]+)"",""level"":(?<level>\d+)\}",
            RegexOptions.Compiled | RegexOptions.CultureInvariant);

        internal static DeviceState ReadProduct(int productId, string name, DeviceKind kind)
        {
            DeviceState missing = new DeviceState
            {
                Name = name,
                Kind = kind,
                IsOnline = false,
                Source = "Razer Synapse (lokalny log)",
                Error = "Brak świeżego zdarzenia baterii."
            };

            string local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            string logDirectory = Path.Combine(local, "Razer", "RazerAppEngine", "User Data", "Logs");
            if (!Directory.Exists(logDirectory)) return missing;

            string pattern = string.Format("products_{0}_mw*.log", productId);
            string[] files;
            try { files = Directory.GetFiles(logDirectory, pattern); }
            catch (IOException ex) { missing.Error = ex.Message; return missing; }

            Array.Sort(files, delegate(string left, string right)
            {
                return File.GetLastWriteTimeUtc(right).CompareTo(File.GetLastWriteTimeUtc(left));
            });

            BatteryRecord latest = null;
            int limit = Math.Min(files.Length, 6);
            for (int i = 0; i < limit; i++)
            {
                BatteryRecord candidate = ReadLatestEvent(files[i]);
                if (candidate != null && (latest == null || candidate.Time > latest.Time)) latest = candidate;
            }
            if (latest == null) return missing;

            bool fresh = DateTime.Now - latest.Time <= TimeSpan.FromMinutes(10);
            bool off = string.Equals(latest.Status, "off", StringComparison.OrdinalIgnoreCase);
            return new DeviceState
            {
                Name = name,
                Kind = kind,
                Level = latest.Level,
                IsCharging = string.Equals(latest.Status, "Charging", StringComparison.OrdinalIgnoreCase),
                IsOnline = fresh && !off,
                IsStale = !fresh,
                UpdatedAt = latest.Time,
                Source = "Razer Synapse (lokalny log)",
                Error = fresh ? string.Empty : "Ostatni odczyt jest nieaktualny."
            };
        }

        internal static BatteryRecord ParseLatest(string text)
        {
            if (string.IsNullOrEmpty(text)) return null;
            MatchCollection matches = BatteryEvent.Matches(text);
            BatteryRecord latest = null;
            foreach (Match match in matches)
            {
                DateTime time;
                int level;
                if (!DateTime.TryParseExact(match.Groups["time"].Value, "yyyy/MM/dd HH:mm:ss.fff", CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal, out time)) continue;
                if (!int.TryParse(match.Groups["level"].Value, NumberStyles.None, CultureInfo.InvariantCulture, out level)) continue;
                if (level < 0 || level > 100) continue;
                BatteryRecord current = new BatteryRecord(time, level, match.Groups["status"].Value);
                if (latest == null || current.Time > latest.Time) latest = current;
            }
            return latest;
        }

        private static BatteryRecord ReadLatestEvent(string path)
        {
            try
            {
                using (FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
                {
                    long start = Math.Max(0, stream.Length - TailBytes);
                    stream.Position = start;
                    int length = checked((int)(stream.Length - start));
                    byte[] bytes = new byte[length];
                    int total = 0;
                    while (total < length)
                    {
                        int read = stream.Read(bytes, total, length - total);
                        if (read == 0) break;
                        total += read;
                    }
                    return ParseLatest(Encoding.UTF8.GetString(bytes, 0, total));
                }
            }
            catch (IOException) { return null; }
            catch (UnauthorizedAccessException) { return null; }
        }

        internal sealed class BatteryRecord
        {
            internal readonly DateTime Time;
            internal readonly int Level;
            internal readonly string Status;

            internal BatteryRecord(DateTime time, int level, string status)
            {
                Time = time;
                Level = level;
                Status = status;
            }
        }
    }
}

