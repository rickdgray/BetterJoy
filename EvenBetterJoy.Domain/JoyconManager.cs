using Microsoft.Extensions.Options;
using Microsoft.Extensions.Logging;
using EvenBetterJoy.Domain.Services;
using EvenBetterJoy.Domain.Models;
using EvenBetterJoy.Domain.Hid;
using EvenBetterJoy.Domain.VirtualController;

namespace EvenBetterJoy.Domain
{
    public class JoyconManager : IJoyconManager
    {
        private readonly Dictionary<string, Joycon> joycons;

        private readonly IHidService hidService;
        private readonly IHidGuardianService hidGuardianService;
        private readonly ICommunicationService communicationService;
        private readonly IVirtualControllerService virtualControllerService;
        private readonly ILogger logger;
        private readonly ILogger joyconLogger;
        private readonly Settings settings;

        public JoyconManager(
            IHidService hidService,
            IHidGuardianService hidGuardianService,
            ICommunicationService communicationService,
            IVirtualControllerService virtualControllerService,
            ILogger<JoyconManager> logger,
            IOptions<Settings> settings,
            IServiceProvider serviceProvider)
        {
            this.hidService = hidService;
            this.hidGuardianService = hidGuardianService;
            this.communicationService = communicationService;
            this.virtualControllerService = virtualControllerService;
            this.logger = logger;
            this.settings = settings.Value;

            //hold reference to pass to joycons since they are not dependency injected
            joyconLogger = serviceProvider.GetService(typeof(ILogger<Joycon>)) as ILogger<Joycon>;

            joycons = new Dictionary<string, Joycon>();
        }

        public async Task Start(CancellationToken cancellationToken)
        {
            while(!cancellationToken.IsCancellationRequested)
            {
                CleanUpDropped();
                CheckForNewControllers(cancellationToken);
                await Task.Delay(settings.ControllerScanRate, cancellationToken);
            }
        }

        private void CleanUpDropped()
        {
            var disconnected = new List<string>();
            foreach ((var serialNumber, var joycon) in joycons)
            {
                if (joycon.State == ControllerState.DROPPED)
                {
                    if (joycon.Other != null)
                    {
                        // The other of the other is the joycon itself
                        joycon.Other.Other = null;
                    }

                    joycon.Detach(true);
                    disconnected.Add(serialNumber);

                    logger.LogInformation("Removed dropped controller. Can be reconnected.");
                }
            }

            foreach (var serialNumber in disconnected)
            {
                joycons.Remove(serialNumber);
            }
        }

        public void CheckForNewControllers(CancellationToken cancellationToken)
        {
            var foundNew = false;
            foreach ((var productId, var serialNumber) in hidService.GetAllNintendoControllers())
            {
                if ((ControllerType)productId == ControllerType.UNKNOWN)
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
                
                var newJoycon = new Joycon(
                    hidService, communicationService, virtualControllerService.Get(),
                    joyconLogger, settings, productId, serialNumber, joycons.Count);

                foundNew = foundNew || joycons.TryAdd(serialNumber, newJoycon);
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

                        if (unjoined.virtualController != null)
                        {
                            try
                            {
                                unjoined.virtualController.Disconnect();
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
                    if (joycon.virtualController != null)
                    {
                        joycon.virtualController.Connect();
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
                    joycon.Begin(cancellationToken);
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

                if (joycon.virtualController != null)
                {
                    joycon.virtualController.Disconnect();
                }
            }
        }
    }
}
