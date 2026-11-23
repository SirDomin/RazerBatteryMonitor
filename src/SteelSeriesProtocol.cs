using System;

namespace RazerBatteryMonitor
{
    internal static class SteelSeriesProtocol
    {
        internal const byte StatusOpcode = 0xB0;

        internal static byte[] BuildStatusRequest(int reportLength)
        {
            if (reportLength < 2) throw new ArgumentOutOfRangeException("reportLength");
            byte[] request = new byte[reportLength];
            request[0] = 0;
            request[1] = StatusOpcode;
            return request;
        }

        internal static Status DecodeStatus(byte[] report, int length)
        {
            if (report == null) throw new ArgumentNullException("report");
            if (length < 4 || length > report.Length) throw new InvalidOperationException("SteelSeries status reply is too short.");

            int offset;
            if (report[0] == StatusOpcode) offset = 0;
            else if (length >= 5 && report[0] == 0 && report[1] == StatusOpcode) offset = 1;
            else throw new InvalidOperationException("This is not a SteelSeries battery status reply.");
            if (length < offset + 4) throw new InvalidOperationException("SteelSeries status reply has no battery fields.");

            int level = report[offset + 2];
            if (level < 0 || level > 100) throw new InvalidOperationException("SteelSeries returned an invalid battery percentage.");
            byte charge = report[offset + 3];
            return new Status(report[offset + 1] == 3 && charge != 0, level, charge == 1 || charge == 2);
        }

        internal sealed class Status
        {
            internal readonly bool IsConnected;
            internal readonly int Level;
            internal readonly bool IsCharging;

            internal Status(bool connected, int level, bool charging)
            {
                IsConnected = connected;
                Level = level;
                IsCharging = charging;
            }
        }
    }
}
