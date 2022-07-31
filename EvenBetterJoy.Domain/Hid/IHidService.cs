namespace EvenBetterJoy.Domain.Hid
{
    public interface IHidService
    {
        void Initialize();
        void CleanUp();
        IntPtr GetDeviceInfoListHead();
        void ReleaseDeviceInfoLinkedList(IntPtr deviceListHead);
        DeviceInfo GetDeviceInfo(IntPtr device);
        IntPtr OpenDevice(ushort productId, string serialNumber);
        void Write(IntPtr device, byte[] data, uint? length = null);
        byte[] Read(IntPtr device, int? milliseconds = null);
        void SetDeviceNonblocking(IntPtr device, bool enable = true);
        void CloseDevice(IntPtr device);
        string GetError(IntPtr device);
    }
}
