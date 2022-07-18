using Microsoft.Extensions.Logging;
using Nefarius.ViGEm.Client;

namespace EvenBetterJoy.Services
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
            try
            {
                //TODO: can we DI this?
                //https://github.com/ViGEm/ViGEm.NET
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
