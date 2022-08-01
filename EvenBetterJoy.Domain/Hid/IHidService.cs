namespace EvenBetterJoy.Domain.Hid
{
    public interface IHidService
    {
        void Initialize();
        void CleanUp();
        List<ControllerInfo> GetAllNintendoControllers();
        IntPtr OpenDevice(int productId, string serialNumber);
        void Write(IntPtr device, byte[] data, int? length = null);
        byte[] Read(IntPtr device, int? milliseconds = null);
        void SetDeviceNonblocking(IntPtr device, bool enable = true);
        void CloseDevice(IntPtr device);
        string GetError(IntPtr device);
    }
}
