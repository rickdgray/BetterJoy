using Microsoft.Extensions.Options;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
using System.Timers;
using EvenBetterJoy.Domain.Services;
using EvenBetterJoy.Domain.Models;
using EvenBetterJoy.Domain.Hid;

namespace EvenBetterJoy.Terminal
{
    public class JoyconManager : IJoyconManager
    {
        public bool EnableIMU = true;
        public bool EnableLocalize = false;

        private readonly ConcurrentDictionary<string, Joycon> joycons;

        System.Timers.Timer joyconPoller;

        private readonly IHidService deviceService;
        private readonly IHidGuardianService hidGuardianService;
        private readonly ICommunicationService communicationService;
        private readonly IVirtualGamepadService virtualGamepadService;
        private readonly ILogger logger;
        private readonly ILogger joyconLogger;
        private readonly Settings settings;

        public JoyconManager(
            IHidService deviceService,
            IHidGuardianService hidGuardianService,
            ICommunicationService communicationService,
            IVirtualGamepadService virtualGamepadService,
            ILogger<JoyconManager> logger,
            IOptions<Settings> settings,
            IServiceProvider serviceProvider)
        {
            this.deviceService = deviceService;
            this.hidGuardianService = hidGuardianService;
            this.communicationService = communicationService;
            this.virtualGamepadService = virtualGamepadService;
            this.logger = logger;
            this.settings = settings.Value;

            //hold reference to pass to joycons since they are not dependency injected
            joyconLogger = serviceProvider.GetService(typeof(ILogger<Joycon>)) as ILogger<Joycon>;

            joycons = new ConcurrentDictionary<string, Joycon>();
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
                joycons.TryRemove(disconnectedJoycon.serial_number, out _);
            }
        }

        public void CheckForNewControllers()
        {
            var deviceListHead = deviceService.GetDeviceInfoListHead();
            
            bool foundNew = false;
            var currentDevice = deviceListHead;
            while (currentDevice != IntPtr.Zero)
            {
                var current = deviceService.GetDeviceInfo(currentDevice);

                var controllerType = (ControllerType)current.product_id;
                if (controllerType == ControllerType.UNKNOWN)
                {
                    currentDevice = current.next;
                    continue;
                }

                //TODO: this check may be unnecessary
                if (current.serial_number == null)
                {
                    currentDevice = current.next;
                    continue;
                }
                
                if (joycons.ContainsKey(current.serial_number))
                {
                    currentDevice = current.next;
                    continue;
                }

                if (settings.UseHidg)
                {
                    hidGuardianService.Block(current.path);
                }

                var handle = deviceService.OpenDevice(current.product_id, current.serial_number);
                if (handle == IntPtr.Zero)
                {
                    logger.LogError("Unable to open device.");
                    currentDevice = current.next;
                    continue;
                }

                deviceService.SetDeviceNonblocking(handle);
                
                foundNew = foundNew || joycons.TryAdd(current.serial_number, new Joycon(deviceService, communicationService,
                    virtualGamepadService.Get(), joyconLogger, settings, handle, EnableIMU, EnableLocalize & EnableIMU,
                    controllerType, current.serial_number, joycons.Count));

                currentDevice = current.next;
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
                    if (joycon.Type != ControllerType.LEFT_JOYCON && joycon.Type != ControllerType.RIGHT_JOYCON)
                    {
                        continue;
                    }

                    if (joycon.Other != null && joycon.Other != joycon)
                    {
                        continue;
                    }

                    // Otherwise, iterate through and find the Joycon with the lowest
                    // id that has not been attached already (Does not include self)
                    if (temp == null)
                    {
                        temp = joycon;
                    }
                    else if (joycon.Type != temp.Type && joycon.Other == null)
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
                                //TODO: don't use exception to handle this
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
                                //TODO: don't use exception to handle this
                                // it wasn't connected in the first place, go figure
                            }
                        }

                        temp = null;
                    }
                }
            }

            deviceService.ReleaseDeviceInfoList(deviceListHead);

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
                        deviceService.SetDeviceNonblocking(joycon.Handle, false);
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
