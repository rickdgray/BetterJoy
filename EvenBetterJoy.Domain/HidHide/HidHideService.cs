using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;
using PInvoke;

namespace EvenBetterJoy.Domain.HidHide
{
    public class HidHideService : IHidHideService
    {
        //https://vigem.org/projects/HidHide/API-Documentation/

        private const uint IOCTL_GET_WHITELIST = 0x80016000;
        private const uint IOCTL_SET_WHITELIST = 0x80016004;
        private const uint IOCTL_GET_BLACKLIST = 0x80016008;
        private const uint IOCTL_SET_BLACKLIST = 0x8001600C;
        private const uint IOCTL_GET_ACTIVE = 0x80016010;
        private const uint IOCTL_SET_ACTIVE = 0x80016014;

        ILogger logger;

        public HidHideService(ILogger<HidHideService> logger)
        {
            this.logger = logger;
        }

        public void Block(string path)
        {
            Block(new List<string> { path });
        }

        public void Block(IEnumerable<string> paths)
        {
            using var handle = Kernel32.CreateFile("\\\\.\\HidHide",
                Kernel32.ACCESS_MASK.GenericRight.GENERIC_READ,
                Kernel32.FileShare.FILE_SHARE_READ | Kernel32.FileShare.FILE_SHARE_WRITE,
                IntPtr.Zero, Kernel32.CreationDisposition.OPEN_EXISTING,
                Kernel32.CreateFileFlags.FILE_ATTRIBUTE_NORMAL,
                Kernel32.SafeObjectHandle.Null);

            var buffer = Marshal.AllocHGlobal(sizeof(bool));

            // Enable blocking logic, if not enabled already
            try
            {
                Marshal.WriteByte(buffer, 1);

                // Check return value for success
                Kernel32.DeviceIoControl(
                    handle,
                    unchecked((int)IOCTL_SET_ACTIVE),
                    buffer,
                    sizeof(bool),
                    IntPtr.Zero,
                    0,
                    out _,
                    IntPtr.Zero
                );
            }
            finally
            {
                Marshal.FreeHGlobal(buffer);
            }

            // List of blocked instances
            IList<string> instances = new List<string>();

            // Get existing list of blocked instances
            // This is important to not discard entries other processes potentially made
            // Always get the current list before altering/submitting it
            try
            {
                // Get required buffer size
                // Check return value for success
                Kernel32.DeviceIoControl(
                    handle,
                    unchecked((int)IOCTL_GET_BLACKLIST),
                    IntPtr.Zero,
                    0,
                    IntPtr.Zero,
                    0,
                    out var required,
                    IntPtr.Zero
                );

                buffer = Marshal.AllocHGlobal(required);

                // Get actual buffer content
                // Check return value for success
                Kernel32.DeviceIoControl(
                    handle,
                    unchecked((int)IOCTL_GET_BLACKLIST),
                    IntPtr.Zero,
                    0,
                    buffer,
                    required,
                    out _,
                    IntPtr.Zero
                );

                // Store existing block-list in a more manageable "C#" fashion
                instances = buffer.MultiSzPointerToStringArray(required).ToList();
            }
            finally
            {
                Marshal.FreeHGlobal(buffer);
            }

            // Manipulate block-list and submit it
            try
            {
                buffer = instances
                    .Concat(paths)
                    .Distinct() // Remove duplicates, if any
                    .StringArrayToMultiSzPointer(out var length); // Convert to usable buffer

                // Submit new list
                // Check return value for success
                Kernel32.DeviceIoControl(
                    handle,
                    unchecked((int)IOCTL_SET_BLACKLIST),
                    buffer,
                    length,
                    IntPtr.Zero,
                    0,
                    out _,
                    IntPtr.Zero
                );
            }
            finally
            {
                Marshal.FreeHGlobal(buffer);
            }
        }

        public void Unblock(string path)
        {
            Unblock(new List<string> { path });
        }

        public void Unblock(IEnumerable<string> paths = null)
        {
            if (paths == null)
            {
                //unblock all
            }
        }
    }
}
