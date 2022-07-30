using Microsoft.Extensions.Logging;
using Nefarius.ViGEm.Client;

namespace EvenBetterJoy.Domain.Services
{
    public class VirtualGamepadService : IVirtualGamepadService
    {
        private ViGEmClient virtualGamepad;

        private readonly ILogger logger;

        public VirtualGamepadService(
            ILogger<VirtualGamepadService> logger)
        {
            this.logger = logger;
        }

        public void Start()
        {
            return;
            
            try
            {
                virtualGamepad = new ViGEmClient();
            }
            catch
            {
                logger.LogError("Could not start VigemBus. Make sure drivers are installed correctly.");
            }
        }

        public ViGEmClient Get()
        {
            return virtualGamepad;
        }
    }
}
