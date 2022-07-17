using Microsoft.Extensions.Options;
using Microsoft.Extensions.Logging;
using System.Net.NetworkInformation;
using EvenBetterJoy.Models;
using EvenBetterJoy.Services;

namespace EvenBetterJoy.Terminal
{
    public class EvenBetterJoyApplication : IEvenBetterJoyApplication
    {
        public PhysicalAddress btMAC = new PhysicalAddress(new byte[] { 0, 0, 0, 0, 0, 0 });
        public ViGEmClient emClient;
        public JoyconManager mgr;

        private readonly HttpClient client = new HttpClient();

        private readonly IHidGuardianService hidGuardianService;
        private readonly ICommunicationService communicationService;
        private readonly ILogger logger;
        private readonly Settings settings;

        public EvenBetterJoyApplication(
            IHidGuardianService hidGuardianService,
            ICommunicationService communicationService,
            ILogger<EvenBetterJoyApplication> logger,
            IOptions<Settings> settings)
        {
            this.hidGuardianService = hidGuardianService;
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
                try
                {
                    // Manages emulated XInput
                    emClient = new ViGEmClient();
                }
                catch
                {
                    logger.LogError("Could not start VigemBus. Make sure drivers are installed correctly.");
                }
            }

            foreach (var nic in NetworkInterface.GetAllNetworkInterfaces())
            {
                // Get local BT host MAC
                if (nic.NetworkInterfaceType != NetworkInterfaceType.FastEthernetFx
                    && nic.NetworkInterfaceType != NetworkInterfaceType.Wireless80211
                    && nic.Name.Split()[0] == "Bluetooth")
                {
                    btMAC = nic.GetPhysicalAddress();
                }
            }

            mgr = new JoyconManager();

            mgr.Awake();
            mgr.CheckForNewControllers();
            mgr.Start();

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
            mgr.OnApplicationQuit();
        }

        void SetupDlls()
        {
            var archPath = $"{AppDomain.CurrentDomain.BaseDirectory}";
            var pathVariable = Environment.GetEnvironmentVariable("PATH");
            pathVariable = $"{archPath};{pathVariable}";
            Environment.SetEnvironmentVariable("PATH", pathVariable);
        }
    }
}
