using Microsoft.Extensions.Options;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
using System.Timers;
using EvenBetterJoy.Domain.Services;
using EvenBetterJoy.Domain.Models;
using EvenBetterJoy.Domain.Hid;

namespace EvenBetterJoy.Domain
{
    public class JoyconManager : IJoyconManager
    {
        public bool EnableIMU = true;
        public bool EnableLocalize = false;

        private readonly ConcurrentDictionary<string, Joycon> joycons;

        System.Timers.Timer joyconPoller;

        private readonly IHidService hidService;
        private readonly IHidGuardianService hidGuardianService;
        private readonly ICommunicationService communicationService;
        private readonly IVirtualGamepadService virtualGamepadService;
        private readonly ILogger logger;
        private readonly ILogger joyconLogger;
        private readonly Settings settings;

        public JoyconManager(
            IHidService hidService,
            IHidGuardianService hidGuardianService,
            ICommunicationService communicationService,
            IVirtualGamepadService virtualGamepadService,
            ILogger<JoyconManager> logger,
            IOptions<Settings> settings,
            IServiceProvider serviceProvider)
        {
            this.hidService = hidService;
            this.hidGuardianService = hidGuardianService;
            this.communicationService = communicationService;
            this.virtualGamepadService = virtualGamepadService;
            this.logger = logger;
            this.settings = settings.Value;

            //hold reference to pass to joycons since they are not dependency injected
            joyconLogger = serviceProvider.GetService(typeof(ILogger<Joycon>)) as ILogger<Joycon>;

            joycons = new ConcurrentDictionary<string, Joycon>();
        }

        public async Task Start(CancellationToken cancellationToken)
        {
            joyconPoller = new System.Timers.Timer(2000);
            joyconPoller.Elapsed += PollJoycons;
            joyconPoller.Start();
        }

        private void PollJoycons(object source, ElapsedEventArgs e)
        {
            CleanUpDropped();

            if (settings.ProgressiveScan)
            {
                CheckForNewControllers();
            }
        }

        private void CleanUpDropped()
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
            var foundNew = false;
            foreach ((var productId, var serialNumber) in hidService.GetAllNintendoControllers())
            {
                var controllerType = (ControllerType)productId;
                if (controllerType == ControllerType.UNKNOWN)
                {
                    continue;
                }

                if (joycons.ContainsKey(serialNumber))
                {
                    continue;
                }

                if (settings.UseHidg)
                {
                    //TODO: revisit this later; try not to use path
                    //hidGuardianService.Block(current.path);
                }

                var handle = hidService.OpenDevice(productId, serialNumber);
                if (handle == IntPtr.Zero)
                {
                    logger.LogError("Unable to open device.");
                    continue;
                }

                hidService.SetDeviceNonblocking(handle);

                foundNew = foundNew || joycons.TryAdd(serialNumber, new Joycon(hidService, communicationService,
                    virtualGamepadService.Get(), joyconLogger, settings, handle, EnableIMU, EnableLocalize & EnableIMU,
                    controllerType, serialNumber, joycons.Count));
            }

            if (foundNew)
            {
                //TODO: switch this to a queue to handle finding same-handed joycons
                Joycon unjoined = null;
                foreach ((_, Joycon joycon) in joycons)
                {
                    // skip if not a joycon
                    if (joycon.Type != ControllerType.LEFT_JOYCON && joycon.Type != ControllerType.RIGHT_JOYCON)
                    {
                        continue;
                    }

                    // skip if already joined
                    if (joycon.Other != null)
                    {
                        continue;
                    }
                    
                    // first unjoined found; hold reference
                    if (unjoined == null)
                    {
                        unjoined = joycon;
                        continue;
                    }

                    // second unjoined found but both are same-handed
                    if (joycon.Type == unjoined.Type)
                    {
                        continue;
                    }

                    // second unjoined found; join them
                    if (joycon.Other == null)
                    {
                        unjoined.Other = joycon;
                        joycon.Other = unjoined;

                        if (unjoined.out_xbox != null)
                        {
                            try
                            {
                                unjoined.out_xbox.Disconnect();
                            }
                            catch
                            {
                                //TODO: don't use exception to handle this
                                // it wasn't connected in the first place, go figure
                            }
                        }
                        if (unjoined.out_ds4 != null)
                        {
                            try
                            {
                                unjoined.out_ds4.Disconnect();
                            }
                            catch
                            {
                                //TODO: don't use exception to handle this
                                // it wasn't connected in the first place, go figure
                            }
                        }

                        unjoined = null;
                    }
                }
            }

            foreach ((_, Joycon joycon) in joycons)
            {
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
                        hidService.SetDeviceNonblocking(joycon.Handle, false);
                        joycon.Attach();
                    }
                    catch
                    {
                        joycon.State = ControllerState.DROPPED;
                        continue;
                    }

                    joycon.SetHomeLight(settings.HomeLedOn);
                    var token = joycon.Begin();
                }
            }
        }

        public void Stop(CancellationToken cancellationToken)
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
