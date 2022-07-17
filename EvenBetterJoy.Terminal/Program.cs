using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using EvenBetterJoy.Services;
using System.Globalization;
using System.Net.NetworkInformation;
using EvenBetterJoy.Domain;

namespace EvenBetterJoy.Terminal
{
    internal class Program
    {
        static void Main(string[] args)
        {
            //TODO: arg parser
            
            using IHost host = Host.CreateDefaultBuilder()
                .ConfigureServices(services =>
                {
                    services.AddSingleton<ISettingsService, SettingsService>();
                })
                .Build();

            //TODO: this should probably be culture independent
            // Setting the culturesettings so float gets parsed correctly
            CultureInfo.CurrentCulture = new CultureInfo("en-US", false);

            SetupDlls();
        }

        public static void Start()
        {
            var pid = Environment.ProcessId.ToString();

            if (useHIDG)
            {
                Console.WriteLine("HidGuardian is enabled.");
                try
                {
                    var HidCerberusService = new ServiceController("HidCerberus Service");
                    if (HidCerberusService.Status == ServiceControllerStatus.Stopped)
                    {
                        Console.WriteLine("HidGuardian was stopped. Starting...");
                        HidCerberusService.Start();
                    }
                }
                catch
                {
                    Console.WriteLine("Unable to start HidGuardian - everything should work fine without it, but if you need it, install it properly as admin.");
                    useHIDG = false;
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
                        Console.WriteLine("Unable to purge whitelist.");
                        useHIDG = false;
                    }
                }

                try
                {
                    // add BetterJoyForCemu to allowed processes 
                    response = (HttpWebResponse)WebRequest.Create(@"http://localhost:26762/api/v1/hidguardian/whitelist/add/" + pid).GetResponse();
                }
                catch
                {
                    Console.WriteLine("Unable to add program to whitelist.");
                    useHIDG = false;
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
                    Console.WriteLine("Could not start VigemBus. Make sure drivers are installed correctly.");
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

            Console.WriteLine("All systems go.");
        }

        public static void Stop()
        {
            if (useHIDG)
            {
                try
                {
                    HttpWebResponse response = (HttpWebResponse)WebRequest.Create(@"http://localhost:26762/api/v1/hidguardian/whitelist/remove/" + pid).GetResponse();
                }
                catch
                {
                    Console.WriteLine("Unable to remove program from whitelist.");
                }
            }

            if (bool.Parse(ConfigurationManager.AppSettings["PurgeAffectedDevices"]) && useHIDG)
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

        static void SetupDlls()
        {
            var archPath = $"{AppDomain.CurrentDomain.BaseDirectory}";
            var pathVariable = Environment.GetEnvironmentVariable("PATH");
            pathVariable = $"{archPath};{pathVariable}";
            Environment.SetEnvironmentVariable("PATH", pathVariable);
        }
    }
}