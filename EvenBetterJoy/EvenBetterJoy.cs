using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using EvenBetterJoy.Domain;
using EvenBetterJoy.Domain.Hid;
using EvenBetterJoy.Domain.VirtualController;
using EvenBetterJoy.Domain.HidHide;
using EvenBetterJoy.Domain.Communication;

namespace EvenBetterJoy
{
    internal class EvenBetterJoy : BackgroundService
    {
        private readonly IJoyconManager joyconManager;
        private readonly IHidService hidService;
        private readonly IHidHideService hidHideService;
        private readonly IVirtualControllerService virtualControllerService;
        private readonly ICommunicationService communicationService;
        private readonly ILogger logger;

        public EvenBetterJoy(
            IJoyconManager joyconManager,
            IHidService hidService,
            IHidHideService hidHideService,
            IVirtualControllerService virtualControllerService,
            ICommunicationService communicationService,
            ILogger<EvenBetterJoy> logger)
        {
            this.joyconManager = joyconManager;
            this.hidService = hidService;
            this.hidHideService = hidHideService;
            this.virtualControllerService = virtualControllerService;
            this.communicationService = communicationService;
            this.logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            logger.LogDebug("Starting application.");
            hidService.Initialize();
            virtualControllerService.Start();

            communicationService.Start();

            logger.LogInformation("Dependencies initialized.");

            await joyconManager.Start(stoppingToken);
        }

        public override Task StopAsync(CancellationToken cancellationToken)
        {
            logger.LogDebug("Stopping application.");
            hidService.CleanUp();
            //TODO: unblock all
            //hidHideService.Unblock();
            communicationService.Stop();
            joyconManager.Stop(cancellationToken);

            return Task.CompletedTask;
        }
    }
}
