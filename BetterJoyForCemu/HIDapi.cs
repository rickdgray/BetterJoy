using System;
using System.Runtime.InteropServices;

namespace EvenBetterJoy
{
    public class HidApi
    {
        const string dll = "hidapi.dll";

        public struct HidDeviceInfo
        {
            [MarshalAs(UnmanagedType.LPStr)]
            public string path;
            public ushort vendor_id;
            public ushort product_id;
            [MarshalAs(UnmanagedType.LPWStr)]
            public string serial_number;
            public ushort release_number;
            [MarshalAs(UnmanagedType.LPWStr)]
            public string manufacturer_string;
            [MarshalAs(UnmanagedType.LPWStr)]
            public string product_string;
            public ushort usage_page;
            public ushort usage;
            public int interface_number;
            public IntPtr next;
        };

        [DllImport(dll, CallingConvention = CallingConvention.Cdecl, EntryPoint = "hid_init")]
        public static extern int HidInit();

        [DllImport(dll, CallingConvention = CallingConvention.Cdecl, EntryPoint = "hid_exit")]
        public static extern int HidExit();

        [DllImport(dll, CallingConvention = CallingConvention.Cdecl, EntryPoint = "hid_enumerate")]
        public static extern IntPtr HidEnumerate(ushort vendor_id, ushort product_id);

        [DllImport(dll, CallingConvention = CallingConvention.Cdecl, EntryPoint = "hid_free_enumeration")]
        public static extern void HidFreeEnumeration(IntPtr phid_device_info);

        [DllImport(dll, CallingConvention = CallingConvention.Cdecl, EntryPoint = "hid_open_path")]
        public static extern IntPtr HidOpenPath([MarshalAs(UnmanagedType.LPWStr)] string path);

        [DllImport(dll, CallingConvention = CallingConvention.Cdecl, EntryPoint = "hid_write")]
        public static extern int HidWrite(IntPtr device, byte[] data, UIntPtr length);

        [DllImport(dll, CallingConvention = CallingConvention.Cdecl, EntryPoint = "hid_read_timeout")]
        public static extern int HidReadTimeout(IntPtr dev, byte[] data, UIntPtr length, int milliseconds);

        [DllImport(dll, CallingConvention = CallingConvention.Cdecl, EntryPoint = "hid_set_nonblocking")]
        public static extern int HidSetNonblocking(IntPtr device, int nonblock);

        [DllImport(dll, CallingConvention = CallingConvention.Cdecl, EntryPoint = "hid_close")]
        public static extern void HidClose(IntPtr device);
    }
}
