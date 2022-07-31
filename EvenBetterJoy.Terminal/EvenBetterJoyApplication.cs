using Microsoft.Extensions.Options;
using Microsoft.Extensions.Logging;
using EvenBetterJoy.Domain.Services;
using EvenBetterJoy.Domain.Models;
using EvenBetterJoy.Domain.Hid;

namespace EvenBetterJoy.Terminal
{
    public class EvenBetterJoyApplication : IEvenBetterJoyApplication
    {
        private readonly IJoyconManager joyconManager;
        private readonly IHidService hidService;
        private readonly IHidGuardianService hidGuardianService;
        private readonly IVirtualGamepadService virtualGamepadService;
        private readonly ICommunicationService communicationService;
        private readonly ILogger logger;
        private readonly Settings settings;

        public EvenBetterJoyApplication(
            IJoyconManager joyconManager,
            IHidService hidService,
            IHidGuardianService hidGuardianService,
            IVirtualGamepadService virtualGamepadService,
            ICommunicationService communicationService,
            ILogger<EvenBetterJoyApplication> logger,
            IOptions<Settings> settings)
        {
            this.joyconManager = joyconManager;
            this.hidService = hidService;
            this.hidGuardianService = hidGuardianService;
            this.virtualGamepadService = virtualGamepadService;
            this.communicationService = communicationService;
            this.logger = logger;
            this.settings = settings.Value;
        }

        public void Start(CancellationToken cancellationToken)
        {
            hidService.Initialize();

            if (settings.UseHidg)
            {
                logger.LogInformation("HidGuardian is enabled.");
                hidGuardianService.Start();
            }

            if (settings.ShowAsXInput || settings.ShowAsDS4)
            {
                virtualGamepadService.Start();
            }

            joyconManager.Start(cancellationToken);

            communicationService.Start();

            logger.LogInformation("All systems go.");
        }

        public void Stop(CancellationToken cancellationToken)
        {
            hidService.CleanUp();

            if (settings.UseHidg)
            {
                hidGuardianService.Stop();
            }

            communicationService.Stop();
            joyconManager.Stop(cancellationToken);
        }
    }
}
