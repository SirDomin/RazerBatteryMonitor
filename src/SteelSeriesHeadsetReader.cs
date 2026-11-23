using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Win32.SafeHandles;

namespace RazerBatteryMonitor
{
    internal static class SteelSeriesHeadsetReader
    {
        private const ushort SteelSeriesVendorId = 0x1038;
        private const ushort ControlUsagePage = 0xFFC0;
        private static readonly Dictionary<ushort, string> Products = new Dictionary<ushort, string>
        {
            { 0x227E, "SteelSeries Arctis Nova 7 Gen 2" },
            { 0x2298, "SteelSeries Arctis Nova 7P Gen 2" },
            { 0x2258, "SteelSeries Arctis Nova 7X Gen 2" },
            { 0x229E, "SteelSeries Arctis Nova 7X Gen 2" },
            { 0x22AD, "SteelSeries Arctis Nova 7X Gen 2" },
            { 0x22A1, "SteelSeries Arctis Nova 7 (2026)" },
            { 0x22A7, "SteelSeries Arctis Nova 7P (2026)" },
            { 0x22A5, "SteelSeries Arctis Nova 7X (2026)" },
            { 0x22A9, "SteelSeries Arctis Nova 7 Diablo (2026)" }
        };

        internal static IList<DeviceState> ReadSupported()
        {
            List<DeviceState> result = new List<DeviceState>();
            HashSet<string> completed = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (HidInterop.HidDevice device in HidInterop.EnumerateDevices(SteelSeriesVendorId))
            {
                string name;
                if (!Products.TryGetValue(device.ProductId, out name) || completed.Contains(name)) continue;
                if (device.UsagePage != ControlUsagePage || device.Path.IndexOf("&mi_03", StringComparison.OrdinalIgnoreCase) < 0) continue;
                DeviceState state = TryRead(device, name);
                result.Add(state);
                completed.Add(name);
            }
            return result;
        }

        private static DeviceState TryRead(HidInterop.HidDevice device, string name)
        {
            DeviceState state = new DeviceState
            {
                Name = name,
                Kind = DeviceKind.Headset,
                Source = "bezpośredni odczyt HID SteelSeries",
                IsOnline = false
            };
            try
            {
                using (SafeFileHandle handle = HidInterop.OpenOverlapped(device.Path))
                {
                    if (handle == null || handle.IsInvalid) throw new IOException("Nie można otworzyć interfejsu sterującego SteelSeries.");
                    int outputLength = device.OutputReportLength >= 2 ? device.OutputReportLength : 64;
                    int inputLength = device.InputReportLength >= 5 ? device.InputReportLength : 64;
                    using (FileStream stream = new FileStream(handle, FileAccess.ReadWrite, Math.Max(inputLength, 64), true))
                    {
                        byte[] request = SteelSeriesProtocol.BuildStatusRequest(outputLength);
                        stream.Write(request, 0, request.Length);
                        stream.Flush();

                        DateTime deadline = DateTime.UtcNow.AddMilliseconds(900);
                        while (DateTime.UtcNow < deadline)
                        {
                            byte[] response = new byte[inputLength];
                            IAsyncResult pending = stream.BeginRead(response, 0, response.Length, null, null);
                            int wait = Math.Max(1, (int)(deadline - DateTime.UtcNow).TotalMilliseconds);
                            if (!pending.AsyncWaitHandle.WaitOne(wait))
                            {
                                HidInterop.CancelIoEx(handle, IntPtr.Zero);
                                try { stream.EndRead(pending); } catch (IOException) { }
                                throw new TimeoutException("Słuchawki SteelSeries nie odpowiedziały na zapytanie baterii.");
                            }

                            int count = stream.EndRead(pending);
                            if (count == 0) continue;
                            try
                            {
                                SteelSeriesProtocol.Status status = SteelSeriesProtocol.DecodeStatus(response, count);
                                state.IsOnline = status.IsConnected;
                                state.Level = status.IsConnected ? (int?)status.Level : null;
                                state.IsCharging = status.IsConnected && status.IsCharging;
                                state.UpdatedAt = DateTime.Now;
                                if (!status.IsConnected) state.Error = "Nadajnik jest podłączony, ale słuchawki są wyłączone.";
                                return state;
                            }
                            catch (InvalidOperationException) { }
                        }
                    }
                }
                state.Error = "Nie otrzymano pełnego raportu stanu SteelSeries.";
            }
            catch (Exception ex)
            {
                state.Error = ex.Message;
            }
            return state;
        }
    }
}
