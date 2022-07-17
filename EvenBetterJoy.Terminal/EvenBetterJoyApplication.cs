using Microsoft.Extensions.Options;
using Microsoft.Extensions.Logging;
using EvenBetterJoy.Models;

namespace EvenBetterJoy.Terminal
{
    internal class EvenBetterJoyApplication : IEvenBetterJoyApplication
    {
        public PhysicalAddress btMAC = new PhysicalAddress(new byte[] { 0, 0, 0, 0, 0, 0 });
        public UdpServer server;
        public ViGEmClient emClient;

        private readonly HttpClient client = new HttpClient();

        public JoyconManager mgr;
        string pid;

        public List<SController> thirdPartyCons = new List<SController>();

        private IKeyboardEventSource keyboard;
        private IMouseEventSource mouse;

        private readonly ILogger logger;
        private readonly Settings settings;

        public EvenBetterJoyApplication(
            ILogger<EvenBetterJoyApplication> logger,
            IOptions<Settings> settings)
        {
            this.logger = logger;
            this.settings = settings.Value;
        }

        public void Start()
        {
            var pid = Environment.ProcessId.ToString();

            if (settings.UseHidg)
            {
                logger.LogInformation("HidGuardian is enabled.");
                try
                {
                    var HidCerberusService = new ServiceController("HidCerberus Service");
                    if (HidCerberusService.Status == ServiceControllerStatus.Stopped)
                    {
                        logger.LogWarning("HidGuardian was stopped. Starting...");
                        HidCerberusService.Start();
                    }
                }
                catch
                {
                    logger.LogError("Unable to start HidGuardian - everything should work fine without it, but if you need it, install it properly as admin.");
                    settings.UseHidg = false;
                }

                HttpWebResponse response;
                if (bool.Parse(ConfigurationManager.AppSettings["PurgeWhitelist"]))
                {
                    try
                    {
                        // remove all programs allowed to see controller
                        response = (HttpWebResponse)WebRequest.Create(@"http://localhost:26762/api/v1/hidguardian/whitelist/purge/").GetResponse();
                    }
                    catch
                    {
                        logger.LogError("Unable to purge whitelist.");
                        settings.UseHidg = false;
                    }
                }

                try
                {
                    // add BetterJoyForCemu to allowed processes 
                    response = (HttpWebResponse)WebRequest.Create(@"http://localhost:26762/api/v1/hidguardian/whitelist/add/" + pid).GetResponse();
                }
                catch
                {
                    logger.LogError("Unable to add program to whitelist.");
                    settings.UseHidg = false;
                }
            }

            if (bool.Parse(ConfigurationManager.AppSettings["ShowAsXInput"]) || bool.Parse(ConfigurationManager.AppSettings["ShowAsDS4"]))
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

            // a bit hacky
            var partyForm = new _3rdPartyControllers();
            partyForm.CopyCustomControllers();

            mgr = new JoyconManager();

            mgr.Awake();
            mgr.CheckForNewControllers();
            mgr.Start();

            server = new UdpServer(mgr.j);

            server.Start(IPAddress.Parse(ConfigurationManager.AppSettings["IP"]), int.Parse(ConfigurationManager.AppSettings["Port"]));

            logger.LogInformation("All systems go.");
        }

        public void Stop()
        {
            if (settings.UseHidg)
            {
                try
                {
                    HttpWebResponse response = (HttpWebResponse)WebRequest.Create(@"http://localhost:26762/api/v1/hidguardian/whitelist/remove/" + pid).GetResponse();
                }
                catch
                {
                    logger.LogError("Unable to remove program from whitelist.");
                }
            }

            if (bool.Parse(ConfigurationManager.AppSettings["PurgeAffectedDevices"]) && settings.UseHidg)
            {
                try
                {
                    HttpWebResponse r1 = (HttpWebResponse)WebRequest.Create(@"http://localhost:26762/api/v1/hidguardian/affected/purge/").GetResponse();
                }
                catch { }
            }

            server.Stop();
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
