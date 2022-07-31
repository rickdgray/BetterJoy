using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;

namespace EvenBetterJoy.Domain.Hid
{
    public class HidService : IHidService
    {
        private const string DLL = "hidapi.dll";
        
        private const ushort NINTENDO = 0x57e;
        private const ushort ALL = 0x0;

        private readonly ILogger logger;

        public HidService(ILogger<HidService> logger)
        {
            this.logger = logger;
        }

        public void Initialize()
        {
            if (hid_init() != 0)
            {
                throw new Exception(GetError(IntPtr.Zero));
            }

            logger.LogInformation($"HidApi version: {Marshal.PtrToStringAnsi(hid_version_str())} initialized.");
        }

        public void CleanUp()
        {
            if (hid_exit() != 0)
            {
                throw new Exception(GetError(IntPtr.Zero));
            }

            logger.LogInformation("HidApi cleaned up.");
        }

        public IntPtr GetDeviceInfoListHead()
        {
            return hid_enumerate(NINTENDO, ALL);
        }

        public void ReleaseDeviceInfoList(IntPtr deviceList)
        {
            hid_free_enumeration(deviceList);
        }

        public DeviceInfo GetDeviceInfo(IntPtr device)
        {
            return (DeviceInfo)Marshal.PtrToStructure(device, typeof(DeviceInfo));
        }

        public IntPtr OpenDevice(ushort productId, string serialNumber)
        {
            return hid_open(NINTENDO, productId, serialNumber);
        }

        public void Write(IntPtr device, byte[] data, uint? length = null)
        {
            if (hid_write(device, data, new UIntPtr(length ?? Constants.REPORT_LENGTH)) == -1)
            {
                throw new Exception(GetError(device));
            }
        }

        public byte[] Read(IntPtr device, int? milliseconds = null)
        {
            //TODO: need to gracefully handle dropped connections
            var data = new byte[Constants.REPORT_LENGTH];
            if (milliseconds.HasValue)
            {
                if (hid_read_timeout(device, data, new UIntPtr(Constants.REPORT_LENGTH), milliseconds.Value) == -1)
                {
                    throw new Exception(GetError(device));
                }

                return data;
            }

            if (hid_read(device, data, new UIntPtr(Constants.REPORT_LENGTH)) == -1)
            {
                throw new Exception(GetError(device));
            }
            
            return data;
        }

        public void SetDeviceNonblocking(IntPtr device, bool enable = true)
        {
            //TODO: because the whole program used read w/ timeout, I think this was useless the whole time
            if (hid_set_nonblocking(device, enable ? 1 : 0) != 0)
            {
                throw new Exception(GetError(device));
            }
        }

        public void CloseDevice(IntPtr device)
        {
            hid_close(device);
        }

        public string GetError(IntPtr device)
        {
            return Marshal.PtrToStringUni(hid_error(device));
        }

        //Info about libusb's hidapi here:
        //https://github.com/libusb/hidapi/blob/master/hidapi/hidapi.h

        [DllImport(DLL)]
        private static extern int hid_init();

        [DllImport(DLL)]
        private static extern int hid_exit();

        [DllImport(DLL)]
        private static extern IntPtr hid_enumerate(ushort vendor_id, ushort product_id);

        [DllImport(DLL)]
        private static extern void hid_free_enumeration(IntPtr devs);

        [DllImport(DLL)]
        private static extern IntPtr hid_open(ushort vendor_id, ushort product_id, [MarshalAs(UnmanagedType.LPWStr)] string serial_number);

        [DllImport(DLL)]
        private static extern int hid_write(IntPtr dev, byte[] data, UIntPtr length);

        [DllImport(DLL)]
        private static extern int hid_read_timeout(IntPtr dev, byte[] data, UIntPtr length, int milliseconds);

        [DllImport(DLL)]
        private static extern int hid_read(IntPtr dev, byte[] data, UIntPtr length);

        [DllImport(DLL)]
        private static extern int hid_set_nonblocking(IntPtr dev, int nonblock);

        [DllImport(DLL)]
        private static extern int hid_send_feature_report(IntPtr dev, byte[] data, UIntPtr length);

        [DllImport(DLL)]
        private static extern int hid_get_feature_report(IntPtr dev, byte[] data, UIntPtr length);

        [DllImport(DLL)]
        private static extern int hid_get_input_report(IntPtr dev, byte[] data, UIntPtr length);

        [DllImport(DLL)]
        private static extern void hid_close(IntPtr dev);

        [DllImport(DLL)]
        private static extern IntPtr hid_error(IntPtr dev);

        [DllImport(DLL)]
        private static extern IntPtr hid_version_str();
    }
}
