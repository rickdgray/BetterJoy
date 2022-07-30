namespace EvenBetterJoy.Domain.Services
{
    public interface IDeviceService
    {
        IntPtr EnumerateDevice(ushort vendorId, ushort productId);
        void FreeDeviceList(IntPtr deviceList);
        IntPtr OpenDevice(ushort vendorId, ushort productId, string serialNumber);
        void CloseDevice(IntPtr device);
        int Read(IntPtr device, byte[] data, UIntPtr length, int milliseconds);
        int Write(IntPtr device, byte[] data, UIntPtr length);
        int SetDeviceNonblocking(IntPtr device, int nonblocking);
    }
}
