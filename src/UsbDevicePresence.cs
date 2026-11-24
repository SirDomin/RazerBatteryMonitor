using System;
using System.Runtime.InteropServices;
using System.Text;

namespace RazerBatteryMonitor
{
    internal static class UsbDevicePresence
    {
        private const uint DigcfPresent = 0x00000002;
        private const uint DigcfAllClasses = 0x00000004;
        private const int ErrorNoMoreItems = 259;
        private static readonly IntPtr InvalidHandleValue = new IntPtr(-1);

        internal static bool IsPresent(int vendorId, int productId)
        {
            IntPtr devices = SetupDiGetClassDevs(IntPtr.Zero, null, IntPtr.Zero, DigcfPresent | DigcfAllClasses);
            if (devices == InvalidHandleValue) return false;

            try
            {
                for (uint index = 0; ; index++)
                {
                    SpDevinfoData data = new SpDevinfoData();
                    data.Size = Marshal.SizeOf(typeof(SpDevinfoData));
                    if (!SetupDiEnumDeviceInfo(devices, index, ref data))
                    {
                        if (Marshal.GetLastWin32Error() == ErrorNoMoreItems) break;
                        continue;
                    }

                    StringBuilder instanceId = new StringBuilder(512);
                    int requiredSize;
                    if (SetupDiGetDeviceInstanceId(devices, ref data, instanceId, instanceId.Capacity, out requiredSize) &&
                        MatchesInstanceId(instanceId.ToString(), vendorId, productId)) return true;
                }
            }
            catch
            {
                return false;
            }
            finally
            {
                SetupDiDestroyDeviceInfoList(devices);
            }
            return false;
        }

        internal static bool MatchesInstanceId(string instanceId, int vendorId, int productId)
        {
            if (string.IsNullOrEmpty(instanceId)) return false;
            string vendor = "VID_" + vendorId.ToString("X4");
            string product = "PID_" + productId.ToString("X4");
            return instanceId.IndexOf(vendor, StringComparison.OrdinalIgnoreCase) >= 0 &&
                   instanceId.IndexOf(product, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct SpDevinfoData
        {
            internal int Size;
            internal Guid ClassGuid;
            internal uint DevInst;
            internal IntPtr Reserved;
        }

        [DllImport("setupapi.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern IntPtr SetupDiGetClassDevs(IntPtr classGuid, string enumerator, IntPtr parent, uint flags);

        [DllImport("setupapi.dll", SetLastError = true)]
        private static extern bool SetupDiEnumDeviceInfo(IntPtr deviceInfoSet, uint memberIndex, ref SpDevinfoData deviceInfoData);

        [DllImport("setupapi.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern bool SetupDiGetDeviceInstanceId(IntPtr deviceInfoSet, ref SpDevinfoData deviceInfoData,
            StringBuilder deviceInstanceId, int deviceInstanceIdSize, out int requiredSize);

        [DllImport("setupapi.dll", SetLastError = true)]
        private static extern bool SetupDiDestroyDeviceInfoList(IntPtr deviceInfoSet);
    }
}
