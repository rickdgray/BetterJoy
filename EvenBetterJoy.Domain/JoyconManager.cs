using Microsoft.Extensions.Options;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
using System.Net;
using System.Net.NetworkInformation;
using System.Runtime.InteropServices;
using System.Text;
using System.Timers;
using EvenBetterJoy.Models;
using EvenBetterJoy.Services;

namespace EvenBetterJoy.Domain
{
    public class JoyconManager
    {
        public bool EnableIMU = true;
        public bool EnableLocalize = false;

        private const ushort vendor_id = 0x57e;
        private const ushort product_l = 0x2006;
        private const ushort product_r = 0x2007;
        private const ushort product_pro = 0x2009;
        private const ushort product_snes = 0x2017;
        private const ushort product_n64 = 0x2019;

        private readonly ConcurrentDictionary<int, Joycon> joycons;

        System.Timers.Timer joyconPoller;

        private readonly IDeviceService deviceService;
        private readonly ILogger logger;
        private readonly Settings settings;

        public JoyconManager(
            IDeviceService deviceService,
            ILogger<JoyconManager> logger,
            IOptions<Settings> settings)
        {
            this.deviceService = deviceService;
            this.logger = logger;
            this.settings = settings.Value;

            joycons = new ConcurrentDictionary<int, Joycon>();
        }

        public void Start()
        {
            // check for new controllers every 2 seconds
            joyconPoller = new System.Timers.Timer(2000);
            joyconPoller.Elapsed += PollJoycons;
            joyconPoller.Start();
        }

        private void PollJoycons(object source, ElapsedEventArgs e)
        {
            CleanUp();
            
            if (settings.ProgressiveScan)
            {
                CheckForNewControllers();
            }
        }

        private void CleanUp()
        {
            var disconnectedJoycons = new List<Joycon>();
            foreach ((_, Joycon joycon) in joycons)
            {
                if (joycon.state == ControllerState.DROPPED)
                {
                    if (joycon.other != null)
                        joycon.other.other = null; // The other of the other is the joycon itself

                    joycon.Detach(true);
                    disconnectedJoycons.Add(joycon);

                    logger.LogInformation("Removed dropped controller. Can be reconnected.");
                }
            }

            foreach (Joycon disconnectedJoycon in disconnectedJoycons)
            {
                joycons.TryRemove(disconnectedJoycon.GetHashCode(), out _);
            }
        }

        private static ushort TypeToProdId(byte type)
        {
            return type switch
            {
                1 => product_pro,
                2 => product_l,
                3 => product_r,
                _ => 0
            };
        }

        public void CheckForNewControllers()
        {
            // move all code for initializing devices here and well as the initial code from Start()
            bool isLeft = false;
            IntPtr ptr = deviceService.EnumerateDevice(0x0, 0x0);
            IntPtr top_ptr = ptr;

            // Add device to list
            DeviceInfo enumerate;
            bool foundNew = false;
            while (ptr != IntPtr.Zero)
            {
                enumerate = (DeviceInfo)Marshal.PtrToStructure(ptr, typeof(DeviceInfo));

                if (enumerate.serial_number == null)
                {
                    ptr = enumerate.next; // can't believe it took me this long to figure out why USB connections used up so much CPU.
                                          // it was getting stuck in an inf loop here!
                    continue;
                }

                bool validController = (enumerate.product_id == product_l || enumerate.product_id == product_r ||
                                        enumerate.product_id == product_pro || enumerate.product_id == product_snes) && enumerate.vendor_id == vendor_id;

                ushort prod_id = thirdParty == null ? enumerate.product_id : TypeToProdId(thirdParty.type);
                if (prod_id == 0)
                {
                    ptr = enumerate.next; // controller was not assigned a type, but advance ptr anyway
                    continue;
                }

                if (validController && !joycons.Any(j => j.Value.path == enumerate.path))
                {
                    switch (prod_id)
                    {
                        case product_l:
                            isLeft = true;
                            logger.LogInformation("Left Joy-Con connected.");
                            break;
                        case product_r:
                            isLeft = false;
                            logger.LogInformation("Right Joy-Con connected.");
                            break;
                        case product_pro:
                            isLeft = true;
                            logger.LogInformation("Pro controller connected.");
                            break;
                        case product_snes:
                            isLeft = true;
                            logger.LogInformation("SNES controller connected.");
                            break;
                        default:
                            logger.LogInformation("Non Joy-Con Nintendo input device skipped.");
                            break;
                    }

                    // Add controller to block-list for HidGuardian
                    if (settings.UseHidg)
                    {
                        HttpWebRequest request = (HttpWebRequest)WebRequest.Create(@"http://localhost:26762/api/v1/hidguardian/affected/add/");
                        string postData = @"hwids=HID\" + enumerate.path.Split('#')[1].ToUpper();
                        var data = Encoding.UTF8.GetBytes(postData);

                        request.Method = "POST";
                        request.ContentType = "application/x-www-form-urlencoded; charset=UTF-8";
                        request.ContentLength = data.Length;

                        using (var stream = request.GetRequestStream())
                            stream.Write(data, 0, data.Length);

                        try
                        {
                            var response = (HttpWebResponse)request.GetResponse();
                            var responseString = new StreamReader(response.GetResponseStream()).ReadToEnd();
                        }
                        catch
                        {
                            logger.LogError("Unable to add controller to block-list.");
                        }
                    }
                    // -------------------- //

                    IntPtr handle = HidOpenPath(enumerate.path);
                    try
                    {
                        HidSetNonblocking(handle, 1);
                    }
                    catch
                    {
                        form.AppendTextBox("Unable to open path to device - are you using the correct (64 vs 32-bit) version for your PC?\r\n");
                        break;
                    }

                    bool isPro = prod_id == product_pro;
                    bool isSnes = prod_id == product_snes;
                    joycons.Add(new Joycon(handle, EnableIMU, EnableLocalize & EnableIMU, 0.05f, isLeft, enumerate.path, enumerate.serial_number, joycons.Count, isPro, isSnes, thirdParty != null));

                    foundNew = true;

                    byte[] mac = new byte[6];
                    try
                    {
                        for (int n = 0; n < 6; n++)
                            mac[n] = byte.Parse(enumerate.serial_number.Substring(n * 2, 2), System.Globalization.NumberStyles.HexNumber);
                    }
                    catch (Exception e)
                    {
                        logger.LogError($"Unable to parse mac address: {e.Message}");
                    }
                    joycons[joycons.Count - 1].PadMacAddress = new PhysicalAddress(mac);
                }

                ptr = enumerate.next;
            }

            if (foundNew)
            { // attempt to auto join-up joycons on connection
                Joycon temp = null;
                foreach (Joycon v in joycons)
                {
                    // Do not attach two controllers if they are either:
                    // - Not a Joycon
                    // - Already attached to another Joycon (that isn't itself)
                    if (v.isPro || (v.other != null && v.other != v))
                    {
                        continue;
                    }

                    // Otherwise, iterate through and find the Joycon with the lowest
                    // id that has not been attached already (Does not include self)
                    if (temp == null)
                        temp = v;
                    else if (temp.isLeft != v.isLeft && v.other == null)
                    {
                        temp.other = v;
                        v.other = temp;

                        if (temp.out_xbox != null)
                        {
                            try
                            {
                                temp.out_xbox.Disconnect();
                            }
                            catch (Exception e)
                            {
                                // it wasn't connected in the first place, go figure
                            }
                        }
                        if (temp.out_ds4 != null)
                        {
                            try
                            {
                                temp.out_ds4.Disconnect();
                            }
                            catch (Exception e)
                            {
                                // it wasn't connected in the first place, go figure
                            }
                        }
                        temp.out_xbox = null;
                        temp.out_ds4 = null;

                        foreach (Button b in form.con)
                            if (b.Tag == v || b.Tag == temp)
                            {
                                Joycon tt = (b.Tag == v) ? v : (b.Tag == temp) ? temp : v;
                                b.BackgroundImage = tt.isLeft ? Properties.Resources.jc_left : Properties.Resources.jc_right;
                            }

                        temp = null;    // repeat
                    }
                }
            }

            HidApi.HidFreeEnumeration(top_ptr);

            bool on = ConfigurationManager.OpenExeConfiguration(ConfigurationUserLevel.None).AppSettings.Settings["HomeLEDOn"].Value.ToLower() == "true";
            foreach (Joycon jc in joycons)
            { // Connect device straight away
                if (jc.state == Joycon.state_.NOT_ATTACHED)
                {
                    if (jc.out_xbox != null)
                        jc.out_xbox.Connect();
                    if (jc.out_ds4 != null)
                        jc.out_ds4.Connect();

                    try
                    {
                        jc.Attach();
                    }
                    catch (Exception e)
                    {
                        jc.state = Joycon.state_.DROPPED;
                        continue;
                    }

                    jc.SetHomeLight(on);

                    jc.Begin();
                    if (form.allowCalibration)
                    {
                        jc.getActiveData();
                    }
                }
            }
        }

        public void OnApplicationQuit()
        {
            foreach (Joycon v in joycons)
            {
                if (Boolean.Parse(ConfigurationManager.AppSettings["AutoPowerOff"]))
                    v.PowerOff();

                v.Detach();

                if (v.out_xbox != null)
                {
                    v.out_xbox.Disconnect();
                }

                if (v.out_ds4 != null)
                {
                    v.out_ds4.Disconnect();
                }
            }

            joyconPoller.Stop();
            HidApi.HidExit();
        }
    }
}
