using System;
using System.Collections.Generic;
using System.Configuration;
using System.Globalization;
using System.Net;
using System.Net.Http;
using System.Net.NetworkInformation;
using System.ServiceProcess;
using System.Threading;
using System.Windows.Forms;
using Nefarius.ViGEm.Client;
using WindowsInput.Events.Sources;
using EvenBetterJoy.Domain;

namespace EvenBetterJoy
{
    class Program
    {
        public static PhysicalAddress btMAC = new PhysicalAddress(new byte[] { 0, 0, 0, 0, 0, 0 });
        public static UdpServer server;
        public static ViGEmClient emClient;

        private static readonly HttpClient client = new HttpClient();

        public static JoyconManager mgr;
        static string pid;

        static MainForm form;

        static public bool useHIDG = bool.Parse(ConfigurationManager.AppSettings["UseHIDG"]);

        public static List<SController> thirdPartyCons = new List<SController>();

        private static IKeyboardEventSource keyboard;
        private static IMouseEventSource mouse;

        private const string APP_GUID = "1bf709e9-c133-41df-933a-c9ff3f664c7b";
        static void Main()
        {
            using var mutex = new Mutex(false, "Global\\" + APP_GUID);
            if (!mutex.WaitOne(0, false))
            {
                MessageBox.Show("Instance already running.", "EvenBetterJoy");
                return;
            }

            //TODO: this should probably be culture independent
            // Setting the culturesettings so float gets parsed correctly
            CultureInfo.CurrentCulture = new CultureInfo("en-US", false);

            // Set the correct DLL for the current OS
            SetupDlls();

            Application.EnableVisualStyles();
            Application.SetHighDpiMode(HighDpiMode.SystemAware);
            Application.SetCompatibleTextRenderingDefault(false);
            form = new MainForm();
            Application.Run(form);
        }

        public static void Start()
        {
            pid = Environment.ProcessId.ToString();

            if (useHIDG)
            {
                form.console.AppendText("HidGuardian is enabled.\r\n");
                try
                {
                    var HidCerberusService = new ServiceController("HidCerberus Service");
                    if (HidCerberusService.Status == ServiceControllerStatus.Stopped)
                    {
                        form.console.AppendText("HidGuardian was stopped. Starting...\r\n");
                        HidCerberusService.Start();
                    }
                }
                catch
                {
                    form.console.AppendText("Unable to start HidGuardian - everything should work fine without it, but if you need it, install it properly as admin.\r\n");
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
                        form.console.AppendText("Unable to purge whitelist.\r\n");
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
                    form.console.AppendText("Unable to add program to whitelist.\r\n");
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
                    form.console.AppendText("Could not start VigemBus. Make sure drivers are installed correctly.\r\n");
                }
            }

            foreach (var nic in NetworkInterface.GetAllNetworkInterfaces())
            {
                // Get local BT host MAC
                if (nic.NetworkInterfaceType != NetworkInterfaceType.FastEthernetFx && nic.NetworkInterfaceType != NetworkInterfaceType.Wireless80211)
                {
                    if (nic.Name.Split()[0] == "Bluetooth")
                    {
                        btMAC = nic.GetPhysicalAddress();
                    }
                }
            }

            // a bit hacky
            var partyForm = new _3rdPartyControllers();
            partyForm.CopyCustomControllers();

            mgr = new JoyconManager
            {
                form = form
            };
            
            mgr.Awake();
            mgr.CheckForNewControllers();
            mgr.Start();

            server = new UdpServer(mgr.joycons)
            {
                form = form
            };

            server.Start(IPAddress.Parse(ConfigurationManager.AppSettings["IP"]), int.Parse(ConfigurationManager.AppSettings["Port"]));

            // Capture keyboard + mouse events for binding's sake
            keyboard = WindowsInput.Capture.Global.KeyboardAsync();
            keyboard.KeyEvent += OnKeyDown;
            mouse = WindowsInput.Capture.Global.MouseAsync();
            mouse.MouseEvent += OnMouseMove;

            form.console.AppendText("All systems go\r\n");
        }

        private static void OnMouseMove(object sender, EventSourceEventArgs<MouseEvent> e)
        {
            if (e.Data.ButtonDown != null)
            {
                string res_val = Config.GetValue("reset_mouse");
                if (res_val.StartsWith("mse_"))
                    if ((int)e.Data.ButtonDown.Button == int.Parse(res_val.Substring(4)))
                        WindowsInput.Simulate.Events().MoveTo(Screen.PrimaryScreen.Bounds.Width / 2, Screen.PrimaryScreen.Bounds.Height / 2).Invoke();

                res_val = Config.GetValue("active_gyro");
                if (res_val.StartsWith("mse_"))
                    if ((int)e.Data.ButtonDown.Button == int.Parse(res_val.Substring(4)))
                        foreach (var i in mgr.joycons)
                            i.active_gyro = true;
            }

            if (e.Data.ButtonUp != null)
            {
                string res_val = Config.GetValue("active_gyro");
                if (res_val.StartsWith("mse_"))
                    if ((int)e.Data.ButtonUp.Button == int.Parse(res_val.Substring(4)))
                        foreach (var i in mgr.joycons)
                            i.active_gyro = false;
            }
        }

        private static void OnKeyDown(object sender, EventSourceEventArgs<KeyboardEvent> e)
        {
            if (e.Data.KeyDown != null)
            {
                string res_val = Config.GetValue("reset_mouse");
                if (res_val.StartsWith("key_"))
                    if ((int)e.Data.KeyDown.Key == int.Parse(res_val.Substring(4)))
                        WindowsInput.Simulate.Events().MoveTo(Screen.PrimaryScreen.Bounds.Width / 2, Screen.PrimaryScreen.Bounds.Height / 2).Invoke();

                res_val = Config.GetValue("active_gyro");
                if (res_val.StartsWith("key_"))
                    if ((int)e.Data.KeyDown.Key == int.Parse(res_val.Substring(4)))
                        foreach (var i in mgr.joycons)
                            i.active_gyro = true;
            }

            if (e.Data.KeyUp != null)
            {
                string res_val = Config.GetValue("active_gyro");
                if (res_val.StartsWith("key_"))
                    if ((int)e.Data.KeyUp.Key == int.Parse(res_val.Substring(4)))
                        foreach (var i in mgr.joycons)
                            i.active_gyro = false;
            }
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
                    form.console.AppendText("Unable to remove program from whitelist.\r\n");
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

            keyboard.Dispose(); mouse.Dispose();
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
