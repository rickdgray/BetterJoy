using System.Runtime.InteropServices;

namespace EvenBetterJoy.Services
{
    public class HidService: IHidService
    {
        const string DLL = "hidapi.dll";

        public HidService()
        {
            if (hid_init() != 0)
            {
                throw new Exception("Failed to initialize HIDAPI");
            }
        }

        public static void Dispose()
        {
            if (hid_exit() != 0)
            {
                throw new Exception("Failed to exit HIDAPI");
            }
        }

        public static IntPtr EnumerateDevice(ushort vendorId, ushort productId)
        {
            return hid_enumerate(vendorId, productId);
        }

        public static void FreeDeviceList(IntPtr deviceList)
        {
            hid_free_enumeration(deviceList);
        }

        public static IntPtr OpenDevice(string device)
        {
            return hid_open_path(device);
        }

        public static void CloseDevice(IntPtr device)
        {
            hid_close(device);
        }

        public static int Read(IntPtr device, byte[] data, UIntPtr length, int milliseconds)
        {
            return hid_read_timeout(device, data, length, milliseconds);
        }

        public static int Write(IntPtr device, byte[] data, UIntPtr length)
        {
            return hid_write(device, data, length);
        }

        public static int SetDeviceNonblocking(IntPtr device, int nonblocking)
        {
            return hid_set_nonblocking(device, nonblocking);
        }

        [DllImport(DLL, CallingConvention = CallingConvention.Cdecl)]
        private static extern int hid_init();

        [DllImport(DLL, CallingConvention = CallingConvention.Cdecl)]
        private static extern int hid_exit();

        [DllImport(DLL, CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr hid_enumerate(ushort vendor_id, ushort product_id);

        [DllImport(DLL, CallingConvention = CallingConvention.Cdecl)]
        private static extern void hid_free_enumeration(IntPtr phid_device_info);

        [DllImport(DLL, CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr hid_open_path([MarshalAs(UnmanagedType.LPWStr)] string path);

        [DllImport(DLL, CallingConvention = CallingConvention.Cdecl)]
        private static extern int hid_write(IntPtr device, byte[] data, UIntPtr length);

        [DllImport(DLL, CallingConvention = CallingConvention.Cdecl)]
        private static extern int hid_read_timeout(IntPtr dev, byte[] data, UIntPtr length, int milliseconds);

        [DllImport(DLL, CallingConvention = CallingConvention.Cdecl)]
        private static extern int hid_set_nonblocking(IntPtr device, int nonblock);

        [DllImport(DLL, CallingConvention = CallingConvention.Cdecl)]
        private static extern void hid_close(IntPtr device);
    }
}
