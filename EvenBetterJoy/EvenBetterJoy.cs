using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using EvenBetterJoy.Domain;
using EvenBetterJoy.Domain.Services;
using EvenBetterJoy.Domain.Models;
using EvenBetterJoy.Domain.Hid;
using EvenBetterJoy.Domain.VirtualController;

namespace EvenBetterJoy
{
    internal class EvenBetterJoy : BackgroundService
    {
        private readonly IJoyconManager joyconManager;
        private readonly IHidService hidService;
        private readonly IHidGuardianService hidGuardianService;
        private readonly IVirtualControllerService virtualControllerService;
        private readonly ICommunicationService communicationService;
        private readonly ILogger logger;
        private readonly Settings settings;

        public EvenBetterJoy(
            IJoyconManager joyconManager,
            IHidService hidService,
            IHidGuardianService hidGuardianService,
            IVirtualControllerService virtualControllerService,
            ICommunicationService communicationService,
            ILogger<EvenBetterJoy> logger,
            IOptions<Settings> settings)
        {
            this.joyconManager = joyconManager;
            this.hidService = hidService;
            this.hidGuardianService = hidGuardianService;
            this.virtualControllerService = virtualControllerService;
            this.communicationService = communicationService;
            this.logger = logger;
            this.settings = settings.Value;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            logger.LogDebug("Starting application.");
            hidService.Initialize();
            virtualControllerService.Start();

            if (settings.UseHidg)
            {
                logger.LogInformation("HidGuardian is enabled.");
                hidGuardianService.Start();
            }

            communicationService.Start();

            logger.LogInformation("Dependencies initialized.");

            await joyconManager.Start(stoppingToken);
        }

        public override Task StopAsync(CancellationToken cancellationToken)
        {
            logger.LogDebug("Stopping application.");
            hidService.CleanUp();

            if (settings.UseHidg)
            {
                hidGuardianService.Stop();
            }

            communicationService.Stop();
            joyconManager.Stop(cancellationToken);

            return Task.CompletedTask;
        }
    }
}
