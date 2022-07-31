using Microsoft.Extensions.Logging;
using Nefarius.ViGEm.Client;

namespace EvenBetterJoy.Domain.VirtualController
{
    public class VirtualControllerService : IVirtualControllerService
    {
        private ViGEmClient virtualController;

        private readonly ILogger logger;

        public VirtualControllerService(
            ILogger<VirtualControllerService> logger)
        {
            this.logger = logger;
        }

        public void Start()
        {
            try
            {
                virtualController = new ViGEmClient();
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to start virtual controller. Make sure drivers are installed correctly.");
            }
        }

        public ViGEmClient Get()
        {
            return virtualController;
        }
    }
}
