using System.Runtime.InteropServices;

namespace EvenBetterJoy.Domain.Services
{
    public class DeviceService: IDeviceService, IDisposable
    {
        const string DLL = "hidapi.dll";

        public DeviceService()
        {
            if (hid_init() != 0)
            {
                throw new Exception("Failed to initialize HIDAPI");
            }
        }

        public void Dispose()
        {
            GC.SuppressFinalize(this);

            if (hid_exit() != 0)
            {
                throw new Exception("Failed to exit HIDAPI");
            }
        }

        public IntPtr EnumerateDevice(ushort vendorId, ushort productId)
        {
            return hid_enumerate(vendorId, productId);
        }

        public void FreeDeviceList(IntPtr deviceList)
        {
            hid_free_enumeration(deviceList);
        }

        public IntPtr OpenDevice(ushort vendorId, ushort productId, string serialNumber)
        {
            return hid_open(vendorId, productId, serialNumber);
        }

        public void CloseDevice(IntPtr device)
        {
            hid_close(device);
        }

        public int Read(IntPtr device, byte[] data, UIntPtr length, int milliseconds)
        {
            return hid_read_timeout(device, data, length, milliseconds);
        }

        public int Write(IntPtr device, byte[] data, UIntPtr length)
        {
            return hid_write(device, data, length);
        }

        //TODO: switch this to bool? make sure that is valid though
        public int SetDeviceNonblocking(IntPtr device, int nonblocking)
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
        private static extern IntPtr hid_open(ushort vendor_id, ushort product_id, [MarshalAs(UnmanagedType.LPWStr)] string serial_number);
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
