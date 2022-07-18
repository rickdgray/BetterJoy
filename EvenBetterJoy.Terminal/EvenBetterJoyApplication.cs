using Microsoft.Extensions.Options;
using Microsoft.Extensions.Logging;
using EvenBetterJoy.Models;
using EvenBetterJoy.Services;

namespace EvenBetterJoy.Terminal
{
    public class EvenBetterJoyApplication : IEvenBetterJoyApplication
    {
        private readonly IHidGuardianService hidGuardianService;
        private readonly IVirtualGamepadService virtualGamepadService;
        private readonly IJoyconManagerService joyconManagerService;
        private readonly ICommunicationService communicationService;
        private readonly ILogger logger;
        private readonly Settings settings;

        public EvenBetterJoyApplication(
            IHidGuardianService hidGuardianService,
            IVirtualGamepadService virtualGamepadService,
            IJoyconManagerService joyconManagerService,
            ICommunicationService communicationService,
            ILogger<EvenBetterJoyApplication> logger,
            IOptions<Settings> settings)
        {
            this.hidGuardianService = hidGuardianService;
            this.virtualGamepadService = virtualGamepadService;
            this.joyconManagerService = joyconManagerService;
            this.communicationService = communicationService;
            this.logger = logger;
            this.settings = settings.Value;
        }

        public void Start()
        {
            if (settings.UseHidg)
            {
                logger.LogInformation("HidGuardian is enabled.");
                hidGuardianService.Start();
            }

            if (settings.ShowAsXInput || settings.ShowAsDS4)
            {
                virtualGamepadService.Start();
            }

            joyconManagerService.Awake();
            joyconManagerService.CheckForNewControllers();
            joyconManagerService.Start();

            communicationService.Start();

            logger.LogInformation("All systems go.");
        }

        public void Stop()
        {
            if (settings.UseHidg)
            {
                hidGuardianService.Stop();
            }

            communicationService.Stop();
            joyconManagerService.OnApplicationQuit();
        }
    }
}
