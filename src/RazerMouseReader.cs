using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Threading;
using Microsoft.Win32.SafeHandles;

namespace RazerBatteryMonitor
{
    internal static class RazerMouseReader
    {
        private static readonly object IoLock = new object();
        private static readonly Dictionary<ushort, string> Products = new Dictionary<ushort, string>
        {
            { 0x00A4, "Razer Naga V2 Pro (Mouse Dock Pro)" },
            { 0x00A7, "Razer Naga V2 Pro" },
            { 0x00A8, "Razer Naga V2 Pro" },
            { 0x00B6, "Razer DeathAdder V3 Pro" },
            { 0x00B7, "Razer DeathAdder V3 Pro" },
            { 0x00C2, "Razer DeathAdder V3 Pro" },
            { 0x00C3, "Razer DeathAdder V3 Pro" }
        };

        internal static IList<DeviceState> ReadSupported()
        {
            List<DeviceState> result = new List<DeviceState>();
            IList<HidInterop.HidDevice> devices = HidInterop.EnumerateRazerDevices();
            HashSet<string> completed = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (HidInterop.HidDevice device in devices)
            {
                string name;
                if (!Products.TryGetValue(device.ProductId, out name) || completed.Contains(name)) continue;
                DeviceState state = TryRead(device, name);
                if (state.IsOnline)
                {
                    result.Add(state);
                    completed.Add(name);
                }
            }
            return result;
        }

        private static DeviceState TryRead(HidInterop.HidDevice device, string name)
        {
            DeviceState state = new DeviceState
            {
                Name = name,
                Kind = DeviceKind.Mouse,
                Source = "bezpośredni odczyt HID",
                IsOnline = false
            };
            try
            {
                lock (IoLock)
                {
                    using (SafeFileHandle handle = HidInterop.Open(device.Path))
                    {
                        if (handle == null || handle.IsInvalid) return state;
                        byte[] battery = Exchange(handle, 0x80);
                        state.Level = RazerProtocol.DecodeBattery(battery);
                        try { state.IsCharging = RazerProtocol.DecodeCharging(Exchange(handle, 0x84)); }
                        catch (NotSupportedException) { state.IsCharging = false; }
                        state.IsOnline = true;
                        state.UpdatedAt = DateTime.Now;
                    }
                }
            }
            catch (Exception ex)
            {
                state.Error = ex.Message;
            }
            return state;
        }

        private static byte[] Exchange(SafeFileHandle handle, byte commandId)
        {
            byte[] request = RazerProtocol.BuildGetter(commandId);
            for (int attempt = 0; attempt < 3; attempt++)
            {
                byte[] feature = new byte[91];
                Buffer.BlockCopy(request, 0, feature, 1, request.Length);
                if (!HidInterop.HidD_SetFeature(handle, feature, feature.Length))
                {
                    throw new Win32Exception(Marshal.GetLastWin32Error(), "Nie można wysłać zapytania HID. Razer Synapse może zajmować interfejs.");
                }
                for (int read = 0; read < 6; read++)
                {
                    Thread.Sleep(100);
                    byte[] responseBuffer = new byte[91];
                    responseBuffer[0] = 0;
                    if (!HidInterop.HidD_GetFeature(handle, responseBuffer, responseBuffer.Length))
                        throw new Win32Exception(Marshal.GetLastWin32Error(), "Nie można odebrać odpowiedzi HID.");
                    byte[] response = new byte[90];
                    Buffer.BlockCopy(responseBuffer, 1, response, 0, response.Length);
                    if (response[0] == RazerProtocol.StatusBusy) continue;
                    try { return RazerProtocol.Decode(response, commandId); }
                    catch (InvalidOperationException) { break; }
                }
            }
            throw new InvalidOperationException("Urządzenie nie odpowiedziało na bezpieczne zapytanie baterii.");
        }
    }
}

