namespace EvenBetterJoy.Services
{
    public interface IDeviceService
    {
        IntPtr EnumerateDevice(ushort vendorId, ushort productId);
        void FreeDeviceList(IntPtr deviceList);
        IntPtr OpenDevice(string device);
        void CloseDevice(IntPtr device);
        int Read(IntPtr device, byte[] data, UIntPtr length, int milliseconds);
        int Write(IntPtr device, byte[] data, UIntPtr length);
        int SetDeviceNonblocking(IntPtr device, int nonblocking);
    }
}