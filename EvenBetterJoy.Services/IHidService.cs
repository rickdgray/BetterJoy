namespace EvenBetterJoy.Services
{
    public interface IHidService
    {
        static IntPtr EnumerateDevice(ushort vendorId, ushort productId) => throw new NotImplementedException();
        static void FreeDeviceList(IntPtr deviceList) => throw new NotImplementedException();
        static IntPtr OpenDevice(string device) => throw new NotImplementedException();
        static void CloseDevice(IntPtr device) => throw new NotImplementedException();
        static int Read(IntPtr device, byte[] data, UIntPtr length, int milliseconds) => throw new NotImplementedException();
        static int Write(IntPtr device, byte[] data, UIntPtr length) => throw new NotImplementedException();
        static int SetDeviceNonblocking(IntPtr device, int nonblocking) => throw new NotImplementedException();
    }
}