using EvenBetterJoy.Models;
using EvenBetterJoy.Services;
using System.Collections.Concurrent;
using System.Net;
using System.Net.NetworkInformation;
using System.Runtime.InteropServices;
using System.Text;
using System.Timers;

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

        // Array of all connected Joy-Cons
        public ConcurrentBag<Joycon> j { get; private set; }

        System.Timers.Timer controllerCheck;

        IHidService hidService;

        public JoyconManager(IHidService hidService)
        {
            this.hidService = hidService;
        }

        public void Awake()
        {
            j = new ConcurrentBag<Joycon>();
            hidService.HidInit();
        }

        public void Start()
        {
            controllerCheck = new System.Timers.Timer(2000); // check for new controllers every 2 seconds
            controllerCheck.Elapsed += CheckForNewControllersTime;
            controllerCheck.Start();
        }

        bool ControllerAlreadyAdded(string path)
        {
            foreach (Joycon v in j)
                if (v.path == path)
                    return true;
            return false;
        }

        void CleanUp()
        {
            // removes dropped controllers from list
            var rem = new List<Joycon>();
            foreach (Joycon joycon in j)
            {
                if (joycon.state == Joycon.state_.DROPPED)
                {
                    if (joycon.other != null)
                        joycon.other.other = null; // The other of the other is the joycon itself

                    joycon.Detach(true);
                    rem.Add(joycon);

                    foreach (Button b in form.con)
                    {
                        if (b.Enabled & b.Tag == joycon)
                        {
                            b.Invoke(new MethodInvoker(delegate
                            {
                                b.BackColor = System.Drawing.Color.FromArgb(0x00, System.Drawing.SystemColors.Control);
                                b.Enabled = false;
                                b.BackgroundImage = Properties.Resources.cross;
                            }));
                            break;
                        }
                    }

                    form.AppendTextBox("Removed dropped controller. Can be reconnected.\r\n");
                }
            }

            foreach (Joycon v in rem)
                j.Remove(v);
        }

        void CheckForNewControllersTime(object source, ElapsedEventArgs e)
        {
            CleanUp();
            if (Config.IntValue("ProgressiveScan") == 1)
            {
                CheckForNewControllers();
            }
        }

        private static ushort TypeToProdId(byte type)
        {
            switch (type)
            {
                case 1:
                    return product_pro;
                case 2:
                    return product_l;
                case 3:
                    return product_r;
                default:
                    break;
            }
            return 0;
        }

        public void CheckForNewControllers()
        {
            // move all code for initializing devices here and well as the initial code from Start()
            bool isLeft = false;
            IntPtr ptr = HidEnumerate(0x0, 0x0);
            IntPtr top_ptr = ptr;

            HidDeviceInfo enumerate; // Add device to list
            bool foundNew = false;
            while (ptr != IntPtr.Zero)
            {
                SController thirdParty = null;
                enumerate = (HidDeviceInfo)Marshal.PtrToStructure(ptr, typeof(HidDeviceInfo));

                if (enumerate.serial_number == null)
                {
                    ptr = enumerate.next; // can't believe it took me this long to figure out why USB connections used up so much CPU.
                                          // it was getting stuck in an inf loop here!
                    continue;
                }

                bool validController = (enumerate.product_id == product_l || enumerate.product_id == product_r ||
                                        enumerate.product_id == product_pro || enumerate.product_id == product_snes) && enumerate.vendor_id == vendor_id;
                // check list of custom controllers specified
                foreach (SController v in Program.thirdPartyCons)
                {
                    if (enumerate.vendor_id == v.vendor_id && enumerate.product_id == v.product_id && enumerate.serial_number == v.serial_number)
                    {
                        validController = true;
                        thirdParty = v;
                        break;
                    }
                }

                ushort prod_id = thirdParty == null ? enumerate.product_id : TypeToProdId(thirdParty.type);
                if (prod_id == 0)
                {
                    ptr = enumerate.next; // controller was not assigned a type, but advance ptr anyway
                    continue;
                }

                if (validController && !ControllerAlreadyAdded(enumerate.path))
                {
                    switch (prod_id)
                    {
                        case product_l:
                            isLeft = true;
                            form.AppendTextBox("Left Joy-Con connected.\r\n"); break;
                        case product_r:
                            isLeft = false;
                            form.AppendTextBox("Right Joy-Con connected.\r\n"); break;
                        case product_pro:
                            isLeft = true;
                            form.AppendTextBox("Pro controller connected.\r\n"); break;
                        case product_snes:
                            isLeft = true;
                            form.AppendTextBox("SNES controller connected.\r\n"); break;
                        default:
                            form.AppendTextBox("Non Joy-Con Nintendo input device skipped.\r\n"); break;
                    }

                    // Add controller to block-list for HidGuardian
                    if (useHIDG)
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
                            form.AppendTextBox("Unable to add controller to block-list.\r\n");
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
                    j.Add(new Joycon(handle, EnableIMU, EnableLocalize & EnableIMU, 0.05f, isLeft, enumerate.path, enumerate.serial_number, j.Count, isPro, isSnes, thirdParty != null));

                    foundNew = true;
                    j.Last().form = form;

                    if (j.Count < 5)
                    {
                        int ii = -1;
                        foreach (Button v in form.con)
                        {
                            ii++;
                            if (!v.Enabled)
                            {
                                System.Drawing.Bitmap temp;
                                switch (prod_id)
                                {
                                    case (product_l):
                                        temp = Properties.Resources.jc_left_s; break;
                                    case (product_r):
                                        temp = Properties.Resources.jc_right_s; break;
                                    case (product_pro):
                                        temp = Properties.Resources.pro; break;
                                    case (product_snes):
                                        temp = Properties.Resources.snes; break;
                                    default:
                                        temp = Properties.Resources.cross; break;
                                }

                                v.Invoke(new MethodInvoker(delegate
                                {
                                    v.Tag = j.Last(); // assign controller to button
                                    v.Enabled = true;
                                    v.Click += new EventHandler(form.conBtnClick);
                                    v.BackgroundImage = temp;
                                }));

                                form.loc[ii].Invoke(new MethodInvoker(delegate
                                {
                                    form.loc[ii].Tag = v;
                                    form.loc[ii].Click += new EventHandler(form.locBtnClickAsync);
                                }));

                                break;
                            }
                        }
                    }

                    byte[] mac = new byte[6];
                    try
                    {
                        for (int n = 0; n < 6; n++)
                            mac[n] = byte.Parse(enumerate.serial_number.Substring(n * 2, 2), System.Globalization.NumberStyles.HexNumber);
                    }
                    catch (Exception e)
                    {
                        // could not parse mac address
                    }
                    j[j.Count - 1].PadMacAddress = new PhysicalAddress(mac);
                }

                ptr = enumerate.next;
            }

            if (foundNew)
            { // attempt to auto join-up joycons on connection
                Joycon temp = null;
                foreach (Joycon v in j)
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
            foreach (Joycon jc in j)
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
            foreach (Joycon v in j)
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

            controllerCheck.Stop();
            HidApi.HidExit();
        }
    }
}
