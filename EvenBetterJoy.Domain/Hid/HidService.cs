using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;

namespace EvenBetterJoy.Domain.Hid
{
    public class HidService : IHidService
    {
        private const string DLL = "hidapi.dll";

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

        public List<ControllerInfo> GetAllNintendoControllers()
        {
            var allControllers = new List<ControllerInfo>();

            var deviceListHead = hid_enumerate(Constants.NINTENDO_VENDOR_ID, Constants.ALL_PRODUCT_IDS);
            var currentDevice = deviceListHead;
            while (currentDevice != IntPtr.Zero)
            {
                var current = (DeviceInfo)Marshal.PtrToStructure(currentDevice, typeof(DeviceInfo));
                if (current.serial_number != null)
                {
                    allControllers.Add(new ControllerInfo
                    {
                        ProductId = current.product_id,
                        SerialNumber = current.serial_number,
                        Path = current.path
                    });
                }

                currentDevice = current.next;
            }

            hid_free_enumeration(deviceListHead);

            return allControllers;
        }

        public IntPtr OpenDevice(int productId, string serialNumber)
        {
            var handle = hid_open(Constants.NINTENDO_VENDOR_ID, Convert.ToUInt16(productId), serialNumber);
            if (handle == IntPtr.Zero)
            {
                throw new Exception(GetError(handle));
            }
            return handle;
        }

        public void Write(IntPtr device, byte[] data, int? length = null)
        {
            if (length == null)
            {
                //TODO: see if we can just do this
                //length = data.Length;

                length = Constants.REPORT_LENGTH;
            }

            if (hid_write(device, data, new UIntPtr(Convert.ToUInt16(length))) == -1)
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
                if (hid_read_timeout(device, data, new UIntPtr(Convert.ToUInt16(Constants.REPORT_LENGTH)), milliseconds.Value) == -1)
                {
                    throw new Exception(GetError(device));
                }

                return data;
            }

            if (hid_read(device, data, new UIntPtr(Convert.ToUInt16(Constants.REPORT_LENGTH))) == -1)
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
