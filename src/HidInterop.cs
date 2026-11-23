using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace RazerBatteryMonitor
{
    internal static class HidInterop
    {
        private const uint DigcfPresent = 0x00000002;
        private const uint DigcfDeviceInterface = 0x00000010;
        private const uint GenericRead = 0x80000000;
        private const uint GenericWrite = 0x40000000;
        private const uint FileShareRead = 0x00000001;
        private const uint FileShareWrite = 0x00000002;
        private const uint OpenExisting = 3;
        private const uint FileFlagOverlapped = 0x40000000;
        private const int HidpStatusSuccess = 0x00110000;

        [StructLayout(LayoutKind.Sequential)]
        private struct SpDeviceInterfaceData
        {
            public int cbSize;
            public Guid InterfaceClassGuid;
            public int Flags;
            public IntPtr Reserved;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct HiddAttributes
        {
            public int Size;
            public ushort VendorId;
            public ushort ProductId;
            public ushort VersionNumber;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct HidpCaps
        {
            public ushort Usage;
            public ushort UsagePage;
            public ushort InputReportByteLength;
            public ushort OutputReportByteLength;
            public ushort FeatureReportByteLength;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 17)] public ushort[] Reserved;
            public ushort NumberLinkCollectionNodes;
            public ushort NumberInputButtonCaps;
            public ushort NumberInputValueCaps;
            public ushort NumberInputDataIndices;
            public ushort NumberOutputButtonCaps;
            public ushort NumberOutputValueCaps;
            public ushort NumberOutputDataIndices;
            public ushort NumberFeatureButtonCaps;
            public ushort NumberFeatureValueCaps;
            public ushort NumberFeatureDataIndices;
        }

        [DllImport("hid.dll")]
        private static extern void HidD_GetHidGuid(out Guid hidGuid);

        [DllImport("hid.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.U1)]
        private static extern bool HidD_GetAttributes(SafeFileHandle handle, ref HiddAttributes attributes);

        [DllImport("hid.dll")]
        [return: MarshalAs(UnmanagedType.U1)]
        private static extern bool HidD_GetPreparsedData(SafeFileHandle handle, out IntPtr preparsedData);

        [DllImport("hid.dll")]
        [return: MarshalAs(UnmanagedType.U1)]
        private static extern bool HidD_FreePreparsedData(IntPtr preparsedData);

        [DllImport("hid.dll")]
        private static extern int HidP_GetCaps(IntPtr preparsedData, ref HidpCaps capabilities);

        [DllImport("hid.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.U1)]
        internal static extern bool HidD_SetFeature(SafeFileHandle handle, byte[] reportBuffer, int reportBufferLength);

        [DllImport("hid.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.U1)]
        internal static extern bool HidD_GetFeature(SafeFileHandle handle, byte[] reportBuffer, int reportBufferLength);

        [DllImport("setupapi.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern IntPtr SetupDiGetClassDevs(ref Guid classGuid, IntPtr enumerator, IntPtr hwndParent, uint flags);

        [DllImport("setupapi.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool SetupDiEnumDeviceInterfaces(IntPtr deviceInfoSet, IntPtr deviceInfoData, ref Guid interfaceClassGuid, uint memberIndex, ref SpDeviceInterfaceData deviceInterfaceData);

        [DllImport("setupapi.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool SetupDiGetDeviceInterfaceDetail(IntPtr deviceInfoSet, ref SpDeviceInterfaceData deviceInterfaceData, IntPtr detailData, uint detailDataSize, out uint requiredSize, IntPtr deviceInfoData);

        [DllImport("setupapi.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool SetupDiDestroyDeviceInfoList(IntPtr deviceInfoSet);

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern SafeFileHandle CreateFile(string fileName, uint desiredAccess, uint shareMode, IntPtr securityAttributes, uint creationDisposition, uint flagsAndAttributes, IntPtr templateFile);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool CancelIoEx(SafeFileHandle handle, IntPtr overlapped);

        internal static IList<HidDevice> EnumerateRazerDevices()
        {
            return EnumerateDevices(0x1532);
        }

        internal static IList<HidDevice> EnumerateDevices(ushort vendorId)
        {
            List<HidDevice> devices = new List<HidDevice>();
            Guid guid;
            HidD_GetHidGuid(out guid);
            IntPtr set = SetupDiGetClassDevs(ref guid, IntPtr.Zero, IntPtr.Zero, DigcfPresent | DigcfDeviceInterface);
            if (set == new IntPtr(-1)) return devices;
            try
            {
                uint index = 0;
                while (true)
                {
                    SpDeviceInterfaceData data = new SpDeviceInterfaceData();
                    data.cbSize = Marshal.SizeOf(typeof(SpDeviceInterfaceData));
                    if (!SetupDiEnumDeviceInterfaces(set, IntPtr.Zero, ref guid, index, ref data)) break;
                    index++;
                    uint required;
                    SetupDiGetDeviceInterfaceDetail(set, ref data, IntPtr.Zero, 0, out required, IntPtr.Zero);
                    if (required == 0) continue;
                    IntPtr detail = Marshal.AllocHGlobal((int)required);
                    try
                    {
                        Marshal.WriteInt32(detail, IntPtr.Size == 8 ? 8 : 6);
                        if (!SetupDiGetDeviceInterfaceDetail(set, ref data, detail, required, out required, IntPtr.Zero)) continue;
                        string path = Marshal.PtrToStringUni(IntPtr.Add(detail, 4));
                        if (string.IsNullOrEmpty(path)) continue;
                        SafeFileHandle handle = Open(path);
                        if (handle == null || handle.IsInvalid) { if (handle != null) handle.Dispose(); continue; }
                        using (handle)
                        {
                            HiddAttributes attributes = new HiddAttributes();
                            attributes.Size = Marshal.SizeOf(typeof(HiddAttributes));
                            if (!HidD_GetAttributes(handle, ref attributes) || attributes.VendorId != vendorId) continue;
                            HidpCaps caps = new HidpCaps();
                            IntPtr preparsed;
                            if (HidD_GetPreparsedData(handle, out preparsed))
                            {
                                try { if (HidP_GetCaps(preparsed, ref caps) != HidpStatusSuccess) caps = new HidpCaps(); }
                                finally { HidD_FreePreparsedData(preparsed); }
                            }
                            devices.Add(new HidDevice(path, attributes.VendorId, attributes.ProductId, caps.UsagePage, caps.Usage,
                                caps.InputReportByteLength, caps.OutputReportByteLength, caps.FeatureReportByteLength));
                        }
                    }
                    finally { Marshal.FreeHGlobal(detail); }
                }
            }
            finally { SetupDiDestroyDeviceInfoList(set); }
            return devices;
        }

        internal static SafeFileHandle Open(string path)
        {
            SafeFileHandle handle = CreateFile(path, GenericRead | GenericWrite, FileShareRead | FileShareWrite, IntPtr.Zero, OpenExisting, 0, IntPtr.Zero);
            if (!handle.IsInvalid) return handle;
            handle.Dispose();
            return CreateFile(path, 0, FileShareRead | FileShareWrite, IntPtr.Zero, OpenExisting, 0, IntPtr.Zero);
        }

        internal static SafeFileHandle OpenOverlapped(string path)
        {
            return CreateFile(path, GenericRead | GenericWrite, FileShareRead | FileShareWrite, IntPtr.Zero, OpenExisting, FileFlagOverlapped, IntPtr.Zero);
        }

        internal sealed class HidDevice
        {
            internal readonly string Path;
            internal readonly ushort VendorId;
            internal readonly ushort ProductId;
            internal readonly ushort UsagePage;
            internal readonly ushort Usage;
            internal readonly ushort InputReportLength;
            internal readonly ushort OutputReportLength;
            internal readonly ushort FeatureReportLength;

            internal HidDevice(string path, ushort vendorId, ushort productId, ushort usagePage, ushort usage,
                ushort inputReportLength, ushort outputReportLength, ushort featureReportLength)
            {
                Path = path;
                VendorId = vendorId;
                ProductId = productId;
                UsagePage = usagePage;
                Usage = usage;
                InputReportLength = inputReportLength;
                OutputReportLength = outputReportLength;
                FeatureReportLength = featureReportLength;
            }
        }
    }
}

