using Microsoft.Extensions.Options;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
using System.Net;
using System.Net.NetworkInformation;
using System.Runtime.InteropServices;
using System.Text;
using System.Timers;
using System.Globalization;
using EvenBetterJoy.Domain.Services;
using EvenBetterJoy.Domain.Models;

namespace EvenBetterJoy.Terminal
{
    public class JoyconManager : IJoyconManager
    {
        public bool EnableIMU = true;
        public bool EnableLocalize = false;

        private const ushort NINTENDO = 0x57e;
        private const ushort LEFT_JOYCON = 0x2006;
        private const ushort RIGHT_JOYCON = 0x2007;
        private const ushort PRO_CONTROLLER = 0x2009;
        //private const ushort NES_CONTROLLER = 0x????;
        private const ushort SNES_CONTROLLER = 0x2017;
        private const ushort N64_CONTROLLER = 0x2019;

        private readonly ConcurrentDictionary<int, Joycon> joycons;

        System.Timers.Timer joyconPoller;

        private readonly IDeviceService deviceService;
        private readonly ICommunicationService communicationService;
        private readonly IVirtualGamepadService virtualGamepadService;
        private readonly ILogger logger;
        private readonly Settings settings;

        public JoyconManager(
            IDeviceService deviceService,
            ICommunicationService communicationService,
            IVirtualGamepadService virtualGamepadService,
            ILogger<JoyconManager> logger,
            IOptions<Settings> settings)
        {
            this.deviceService = deviceService;
            this.communicationService = communicationService;
            this.virtualGamepadService = virtualGamepadService;
            this.logger = logger;
            this.settings = settings.Value;

            joycons = new ConcurrentDictionary<int, Joycon>();
        }

        public void Start()
        {
            joyconPoller = new System.Timers.Timer(2000);
            joyconPoller.Elapsed += PollJoycons;
            joyconPoller.Start();
        }

        private void PollJoycons(object source, ElapsedEventArgs e)
        {
            CleanUp();

            if (settings.ProgressiveScan)
            {
                CheckForNewControllers();
            }
        }

        private void CleanUp()
        {
            var disconnectedJoycons = new List<Joycon>();
            foreach ((_, Joycon joycon) in joycons)
            {
                if (joycon.State == ControllerState.DROPPED)
                {
                    if (joycon.Other != null)
                    {
                        // The other of the other is the joycon itself
                        joycon.Other.Other = null;
                    }

                    joycon.Detach(true);
                    disconnectedJoycons.Add(joycon);

                    logger.LogInformation("Removed dropped controller. Can be reconnected.");
                }
            }

            foreach (Joycon disconnectedJoycon in disconnectedJoycons)
            {
                joycons.TryRemove(disconnectedJoycon.GetHashCode(), out _);
            }
        }

        private static ushort TypeToProdId(byte type)
        {
            return type switch
            {
                1 => PRO_CONTROLLER,
                2 => LEFT_JOYCON,
                3 => RIGHT_JOYCON,
                4 => SNES_CONTROLLER,
                5 => N64_CONTROLLER,
                _ => 0
            };
        }

        public void CheckForNewControllers()
        {
            bool isLeft = true;
            IntPtr ptr = deviceService.EnumerateDevice(0x0, 0x0);
            IntPtr top_ptr = ptr;

            // Add device to list
            DeviceInfo enumerate;
            bool foundNew = false;
            while (ptr != IntPtr.Zero)
            {
                enumerate = (DeviceInfo)Marshal.PtrToStructure(ptr, typeof(DeviceInfo));

                if (enumerate.serial_number == null)
                {
                    ptr = enumerate.next;
                    continue;
                }

                bool validController = enumerate.vendor_id == NINTENDO
                    && (enumerate.product_id == LEFT_JOYCON
                        || enumerate.product_id == RIGHT_JOYCON
                        || enumerate.product_id == PRO_CONTROLLER
                        || enumerate.product_id == SNES_CONTROLLER
                        || enumerate.product_id == N64_CONTROLLER);

                ushort prod_id = enumerate.product_id;
                if (prod_id == 0)
                {
                    // controller was not assigned a type, but advance ptr anyway
                    ptr = enumerate.next;
                    continue;
                }

                if (validController && !joycons.Any(j => j.Value.path == enumerate.path))
                {
                    switch (prod_id)
                    {
                        case LEFT_JOYCON:
                            logger.LogInformation("Left Joy-Con connected.");
                            break;
                        case RIGHT_JOYCON:
                            isLeft = false;
                            logger.LogInformation("Right Joy-Con connected.");
                            break;
                        case PRO_CONTROLLER:
                            logger.LogInformation("Pro controller connected.");
                            break;
                        case SNES_CONTROLLER:
                            logger.LogInformation("SNES controller connected.");
                            break;
                        case N64_CONTROLLER:
                            logger.LogInformation("N64 controller connected.");
                            break;
                        default:
                            logger.LogInformation("Non Joy-Con Nintendo input device skipped.");
                            break;
                    }

                    // Add controller to block-list for HidGuardian
                    if (settings.UseHidg)
                    {
                        HttpWebRequest request = (HttpWebRequest)WebRequest.Create(@"http://localhost:26762/api/v1/hidguardian/affected/add/");
                        string postData = @"hwids=HID\" + enumerate.path.Split('#')[1].ToUpper();
                        var data = Encoding.UTF8.GetBytes(postData);

                        request.Method = "POST";
                        request.ContentType = "application/x-www-form-urlencoded; charset=UTF-8";
                        request.ContentLength = data.Length;

                        using (var stream = request.GetRequestStream())
                        {
                            stream.Write(data, 0, data.Length);
                        }

                        try
                        {
                            var response = (HttpWebResponse)request.GetResponse();
                            var responseString = new StreamReader(response.GetResponseStream()).ReadToEnd();
                        }
                        catch
                        {
                            logger.LogError("Unable to add controller to block-list.");
                        }
                    }

                    IntPtr handle = deviceService.OpenDevice(enumerate.path);
                    try
                    {
                        deviceService.SetDeviceNonblocking(handle, 1);
                    }
                    catch
                    {
                        logger.LogError("Unable to open path to device - are you using the correct (64 vs 32-bit) version for your PC?");
                        break;
                    }

                    bool isPro = prod_id == PRO_CONTROLLER;
                    bool isSnes = prod_id == SNES_CONTROLLER;
                    bool isN64 = prod_id == N64_CONTROLLER;

                    var joycon = new Joycon(settings, deviceService, communicationService,
                        virtualGamepadService.Get(), handle, EnableIMU, EnableLocalize & EnableIMU, 0.05f,
                        isLeft, enumerate.path, enumerate.serial_number, joycons.Count, isPro, isSnes);

                    joycons.TryAdd(joycon.GetHashCode(), joycon);

                    foundNew = true;

                    byte[] mac = new byte[6];
                    try
                    {
                        for (int n = 0; n < 6; n++)
                        {
                            mac[n] = byte.Parse(enumerate.serial_number.Substring(n * 2, 2), NumberStyles.HexNumber);
                        }
                    }
                    catch (Exception e)
                    {
                        logger.LogError($"Unable to parse mac address: {e.Message}");
                    }
                    joycons[joycons.Count - 1].PadMacAddress = new PhysicalAddress(mac);
                }

                ptr = enumerate.next;
            }

            if (foundNew)
            {
                // attempt to auto join-up joycons on connection
                Joycon temp = null;
                foreach ((_, Joycon joycon) in joycons)
                {
                    // Do not attach two controllers if they are either:
                    // - Not a Joycon
                    // - Already attached to another Joycon (that isn't itself)
                    if (joycon.isPro || (joycon.Other != null && joycon.Other != joycon))
                    {
                        continue;
                    }

                    // Otherwise, iterate through and find the Joycon with the lowest
                    // id that has not been attached already (Does not include self)
                    if (temp == null)
                    {
                        temp = joycon;
                    }
                    else if (temp.isLeft != joycon.isLeft && joycon.Other == null)
                    {
                        temp.Other = joycon;
                        joycon.Other = temp;

                        if (temp.out_xbox != null)
                        {
                            try
                            {
                                temp.out_xbox.Disconnect();
                            }
                            catch
                            {
                                // it wasn't connected in the first place, go figure
                            }
                        }
                        if (temp.out_ds4 != null)
                        {
                            try
                            {
                                temp.out_ds4.Disconnect();
                            }
                            catch
                            {
                                // it wasn't connected in the first place, go figure
                            }
                        }

                        temp = null;
                    }
                }
            }

            deviceService.FreeDeviceList(top_ptr);

            foreach ((_, Joycon joycon) in joycons)
            {
                // Connect device straight away
                if (joycon.State == ControllerState.NOT_ATTACHED)
                {
                    if (joycon.out_xbox != null)
                    {
                        joycon.out_xbox.Connect();
                    }

                    if (joycon.out_ds4 != null)
                    {
                        joycon.out_ds4.Connect();
                    }

                    try
                    {
                        deviceService.SetDeviceNonblocking(joycon.Handle, 0);
                        joycon.Attach();
                    }
                    catch
                    {
                        joycon.State = ControllerState.DROPPED;
                        continue;
                    }

                    joycon.SetHomeLight(settings.HomeLedOn);
                    joycon.Begin();
                }
            }
        }

        public void Stop()
        {
            foreach ((_, Joycon joycon) in joycons)
            {
                if (settings.AutoPowerOff)
                {
                    joycon.PowerOff();
                }

                joycon.Detach();

                if (joycon.out_xbox != null)
                {
                    joycon.out_xbox.Disconnect();
                }

                if (joycon.out_ds4 != null)
                {
                    joycon.out_ds4.Disconnect();
                }
            }

            joyconPoller.Stop();
        }
    }
}
