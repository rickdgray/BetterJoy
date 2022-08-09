using Microsoft.Extensions.Logging;
using System.Diagnostics;
using System.Globalization;
using System.Net.NetworkInformation;
using System.Numerics;
using Nefarius.ViGEm.Client.Targets.Xbox360;
using Nefarius.ViGEm.Client;
using EvenBetterJoy.Domain.Communication;
using EvenBetterJoy.Domain.Hid;
using EvenBetterJoy.Domain.VirtualController;

namespace EvenBetterJoy.Domain.Models
{
    public class Joycon
    {

        public IntPtr Handle { get; private set; }
        public ControllerType Type { get; private set; }
        private readonly string serialNumber;
        //TODO: make set private
        public ControllerState State { get; set; }
        public VirtualController.VirtualController virtualController { get; set; }


        ///////////////////////////


        private Joycon _other = null;
        public Joycon Other
        {
            get
            {
                return _other;
            }
            set
            {
                _other = value;

                // If the other Joycon is itself, the Joycon is sideways
                if (_other == null || _other == this)
                {
                    // Set LED to current Pad ID
                    SetLEDByPlayerNum(padId);
                }
                else
                {
                    // Set LED to current Joycon Pair
                    int lowestPadId = Math.Min(_other.padId, padId);
                    SetLEDByPlayerNum(lowestPadId);
                }
            }
        }
        public bool active_gyro = false;

        private long inactivity = Stopwatch.GetTimestamp();
        
        private bool[] buttons_down = new bool[20];
        private bool[] buttons_up = new bool[20];
        private bool[] buttons = new bool[20];
        private bool[] down_ = new bool[20];
        private long[] buttons_down_timestamp = new long[20];

        private float[] stick = { 0, 0 };
        private float[] stick2 = { 0, 0 };

        private byte[] default_buf = { 0x0, 0x1, 0x40, 0x40, 0x0, 0x1, 0x40, 0x40 };

        private byte[] stick_raw = { 0, 0, 0 };
        private ushort[] stick_cal = { 0, 0, 0, 0, 0, 0 };
        private ushort deadzone;
        private ushort[] stick_precal = { 0, 0 };

        private byte[] stick2_raw = { 0, 0, 0 };
        private ushort[] stick2_cal = { 0, 0, 0, 0, 0, 0 };
        private ushort deadzone2;
        private ushort[] stick2_precal = { 0, 0 };
        
        private bool imu_enabled = false;
        private short[] acc_r = { 0, 0, 0 };
        private short[] acc_neutral = { 0, 0, 0 };
        private short[] acc_sensiti = { 0, 0, 0 };
        private Vector3 acc_g;

        private short[] gyr_r = { 0, 0, 0 };
        private short[] gyr_neutral = { 0, 0, 0 };
        private short[] gyr_sensiti = { 0, 0, 0 };
        private Vector3 gyr_g;

        private float[] cur_rotation; // Filtered IMU data

        private short[] acc_sen = new short[3]{
            16384,
            16384,
            16384
        };
        private short[] gyr_sen = new short[3]{
            18642,
            18642,
            18642
        };

        private short[] pro_hor_offset = { -710, 0, 0 };
        private short[] left_hor_offset = { 0, 0, 0 };
        private short[] right_hor_offset = { 0, 0, 0 };

        private Rumble rumble;

        private byte global_count = 0;

        // For UdpServer
        public int padId = 0;
        public int battery = -1;
        public int model = 2;
        public int constate = 2;
        public int connection = 3;

        public PhysicalAddress PadMacAddress = new PhysicalAddress(new byte[] { 01, 02, 03, 04, 05, 06 });
        public ulong Timestamp = 0;
        public int packetCounter = 0;

        public byte LED { get; private set; } = 0x0;
        public void SetLEDByPlayerNum(int id)
        {
            if (id > 3)
            {
                // No support for any higher than 3 (4 Joycons/Controllers supported in the application normally)
                id = 3;
            }

            if (settings.UseIncrementalLights)
            {
                // Set all LEDs from 0 to the given id to lit
                int ledId = id;
                LED = 0x0;
                do
                {
                    LED |= (byte)(0x1 << ledId);
                } while (--ledId >= 0);
            }
            else
            {
                LED = (byte)(0x1 << id);
            }

            SetPlayerLED(LED);
        }

        private float[] activeData;


        ///////////////////////////


        private readonly GyroHelper gyroHelper;

        private readonly IHidService hidService;
        private readonly ICommunicationService communicationService;
        private readonly ViGEmClient client;
        private readonly ILogger logger;
        private readonly Settings settings;

        public Joycon(IHidService hidService, ICommunicationService communicationService, ViGEmClient client,
            ILogger logger, Settings settings, int productId, string serialNumber, int playerNumber)
        {
            this.hidService = hidService;
            this.communicationService = communicationService;
            this.client = client;
            this.logger = logger;
            this.settings = settings;

            Type = (ControllerType)productId;
            this.serialNumber = serialNumber;
            padId = playerNumber;
            LED = (byte)(0x1 << padId);

            Handle = hidService.OpenDevice(productId, serialNumber);
            hidService.SetDeviceNonblocking(Handle);

            activeData = new float[6];

            byte[] mac = new byte[6];
            for (int n = 0; n < 6; n++)
            {
                mac[n] = byte.Parse(this.serialNumber.Substring(n * 2, 2), NumberStyles.HexNumber);
            }
            PadMacAddress = new PhysicalAddress(mac);

            rumble = new Rumble(new float[] { settings.LowFreqRumble, settings.HighFreqRumble, 0 });
            for (int i = 0; i < buttons_down_timestamp.Length; i++)
            {
                buttons_down_timestamp[i] = -1;
            }
            
            connection = 0x02;

            virtualController = new VirtualController.VirtualController(client, Type);
            if (settings.EnableRumble)
            {
                //virtualController.FeedbackReceived += ReceiveRumble;
            }

            gyroHelper = new GyroHelper(0.005f, settings.AhrsBeta);
        }

        public void ReceiveRumble(Xbox360FeedbackReceivedEventArgs e)
        {
            SetRumble(settings.LowFreqRumble, settings.HighFreqRumble, Math.Max(e.LargeMotor, e.SmallMotor) / (float)255);

            if (Other != null && Other != this)
            {
                Other.SetRumble(settings.LowFreqRumble, settings.HighFreqRumble, Math.Max(e.LargeMotor, e.SmallMotor) / (float)255);
            }
        }

        public void Attach()
        {
            LoadCalibrationData();

            BlinkHomeLight();
            SetLEDByPlayerNum(padId);

            //TODO: need to better document what all this is
            Subcommand(0x40, new byte[] { imu_enabled ? (byte)0x1 : (byte)0x0 }, 1);
            Subcommand(0x48, new byte[] { 0x01 }, 1);

            Subcommand(0x3, new byte[] { 0x30 }, 1);

            hidService.SetDeviceNonblocking(Handle);

            State = ControllerState.ATTACHED;
        }

        private void SetPlayerLED(byte leds)
        {
            Subcommand(0x30, new byte[] { leds }, 1);
        }

        private void BlinkHomeLight()
        {
            byte[] a = Enumerable.Repeat((byte)0xFF, 25).ToArray();
            a[0] = 0x18;
            a[1] = 0x01;
            Subcommand(0x38, a, 25);
        }

        public void SetHomeLight(bool on)
        {
            byte[] a = Enumerable.Repeat((byte)0xFF, 25).ToArray();
            if (on)
            {
                a[0] = 0x1F;
                a[1] = 0xF0;
            }
            else
            {
                a[0] = 0x10;
                a[1] = 0x01;
            }
            Subcommand(0x38, a, 25);
        }

        private void SetHCIState(byte state)
        {
            byte[] a = { state };
            Subcommand(0x06, a, 1);
        }

        public void PowerOff()
        {
            if (State > ControllerState.DROPPED)
            {
                hidService.SetDeviceNonblocking(Handle, false);
                SetHCIState(0x00);
                State = ControllerState.DROPPED;
            }
        }

        private void BatteryChanged()
        {
            if (battery <= 1)
            {
                //TODO: figure out how to alert the user
                //string.Format("Controller {0} ({1}) - low battery notification!", PadId, controllerType == ControllerType.PRO_CONTROLLER ? "Pro Controller" : (controllerType == ControllerType.SNES_CONTROLLER ? "SNES Controller" : (controllerType == ControllerType.LEFT_JOYCON ? "Joycon Left" : "Joycon Right")));
            }
        }

        public void Detach(bool close = false)
        {
            virtualController.Disconnect();

            if (State >= ControllerState.ATTACHED)
            {
                hidService.SetDeviceNonblocking(Handle, false);

                //TODO: this was already commented out; what was it for?
                //Subcommand(0x40, new byte[] { 0x0 }, 1); // disable IMU sensor
                //Subcommand(0x48, new byte[] { 0x0 }, 1); // Would turn off rumble?
            }

            if (close || State > ControllerState.DROPPED)
            {
                hidService.CloseDevice(Handle);
            }

            State = ControllerState.NOT_ATTACHED;
        }

        private byte ts_en;
        private bool ReceiveRaw()
        {
            if (Handle == IntPtr.Zero)
            {
                throw new NullReferenceException("Joycon handle is null");
            }

            var data = hidService.Read(Handle, 5);

            if (data.Length > 0)
            {
                // Process packets as soon as they come
                for (var n = 0; n < 3; n++)
                {
                    ExtractIMUValues(data, n);

                    var lag = (byte)Math.Max(0, data[1] - ts_en - 3);
                    if (n == 0)
                    {
                        // add lag once
                        Timestamp += (ulong)lag * 5000;
                        ProcessButtonsAndStick(data);

                        // process buttons here to have them affect DS4
                        DoThingsWithButtons();

                        int newbat = battery;
                        battery = (data[2] >> 4) / 2;
                        if (newbat != battery)
                        {
                            BatteryChanged();
                        }
                    }

                    Timestamp += 5000;
                    packetCounter++;

                    communicationService.NewReportIncoming(this);
                }

                virtualController.UpdateInput(MapToVirtualControllerInput(this));

                ts_en = data[1];
            }

            return true;
        }

        Dictionary<int, bool> mouse_toggle_btn = new Dictionary<int, bool>();
        private void Simulate(string s, bool click = true, bool up = false)
        {
            //TODO: get rid of this string parsing hack
            //TODO: try out Desktop.Robot for os agnostic key simulation
            //if (s.StartsWith("key_"))
            //{
            //    WindowsInput.Events.KeyCode key = (WindowsInput.Events.KeyCode)Int32.Parse(s.Substring(4));
            //    if (click)
            //    {
            //        WindowsInput.Simulate.Events().Click(key).Invoke();
            //    }
            //    else
            //    {
            //        if (up)
            //        {
            //            WindowsInput.Simulate.Events().Release(key).Invoke();
            //        }
            //        else
            //        {
            //            WindowsInput.Simulate.Events().Hold(key).Invoke();
            //        }
            //    }
            //}
            //else if (s.StartsWith("mse_"))
            //{
            //    WindowsInput.Events.ButtonCode button = (WindowsInput.Events.ButtonCode)Int32.Parse(s.Substring(4));
            //    if (click)
            //    {
            //        WindowsInput.Simulate.Events().Click(button).Invoke();
            //    }
            //    else
            //    {
            //        if (settings.DragToggle)
            //        {
            //            if (!up)
            //            {
            //                mouse_toggle_btn.TryGetValue((int)button, out bool release);
            //                if (release)
            //                    WindowsInput.Simulate.Events().Release(button).Invoke();
            //                else
            //                    WindowsInput.Simulate.Events().Hold(button).Invoke();
            //                mouse_toggle_btn[(int)button] = !release;
            //            }
            //        }
            //        else
            //        {
            //            if (up)
            //            {
            //                WindowsInput.Simulate.Events().Release(button).Invoke();
            //            }
            //            else
            //            {
            //                WindowsInput.Simulate.Events().Hold(button).Invoke();
            //            }
            //        }
            //    }
            //}
        }

        // For Joystick->Joystick inputs
        private void SimulateContinous(int origin, string s)
        {
            if (s.StartsWith("joy_"))
            {
                int button = int.Parse(s.Substring(4));
                buttons[button] |= buttons[origin];
            }
        }

        long lastDoubleClick = -1;
        byte[] sliderVal = new byte[] { 0, 0 };
        private void DoThingsWithButtons()
        {
            //TODO: double check this button mapping
            //int powerOffButton = (int)((controllerType == ControllerType.PRO_CONTROLLER || controllerType != ControllerType.LEFT_JOYCON || Other != null) ? ControllerButton.HOME : ControllerButton.CAPTURE);
            var powerOffButton = Type switch
            {
                ControllerType.LEFT_JOYCON => (int)ControllerButton.CAPTURE,
                ControllerType.RIGHT_JOYCON => (int)ControllerButton.HOME,
                ControllerType.PRO_CONTROLLER => (int)ControllerButton.HOME,
                ControllerType.SNES_CONTROLLER => (int)ControllerButton.HOME,
                ControllerType.N64_CONTROLLER => (int)ControllerButton.HOME,
                _ => throw new Exception("Unknown controller type")
            };

            long timestamp = Stopwatch.GetTimestamp();
            if (settings.HomeLongPowerOff && buttons[powerOffButton])
            {
                if ((timestamp - buttons_down_timestamp[powerOffButton]) / 10000 > 2000.0)
                {
                    if (Other != null)
                    {
                        Other.PowerOff();
                    }

                    PowerOff();
                    return;
                }
            }

            if (settings.ChangeOrientationDoubleClick && buttons_down[(int)ControllerButton.STICK] && lastDoubleClick != -1 && Type != ControllerType.PRO_CONTROLLER)
            {
                if ((buttons_down_timestamp[(int)ControllerButton.STICK] - lastDoubleClick) < 3000000)
                {
                    //TODO: this is disgusting
                    // trigger connection button click
                    //form.conBtnClick(form.con[PadId], EventArgs.Empty);

                    lastDoubleClick = buttons_down_timestamp[(int)ControllerButton.STICK];
                    return;
                }

                lastDoubleClick = buttons_down_timestamp[(int)ControllerButton.STICK];
            }
            else if (settings.ChangeOrientationDoubleClick && buttons_down[(int)ControllerButton.STICK] && Type != ControllerType.PRO_CONTROLLER)
            {
                lastDoubleClick = buttons_down_timestamp[(int)ControllerButton.STICK];
            }

            if (settings.PowerOffInactivity > 0)
            {
                if ((timestamp - inactivity) / 10000 > settings.PowerOffInactivity * 60 * 1000)
                {
                    if (Other != null)
                    {
                        Other.PowerOff();
                    }

                    PowerOff();
                    return;
                }
            }

            //DetectShake();

            if (buttons_down[(int)ControllerButton.CAPTURE])
            {
                Simulate(settings.Capture);
            }

            if (buttons_down[(int)ControllerButton.HOME])
            {
                Simulate(settings.Home);
            }

            SimulateContinous((int)ControllerButton.CAPTURE, settings.Capture);
            SimulateContinous((int)ControllerButton.HOME, settings.Home);

            if (Type == ControllerType.LEFT_JOYCON)
            {
                if (buttons_down[(int)ControllerButton.SL])
                {
                    Simulate(settings.LeftJoyconL, false, false);
                }

                if (buttons_up[(int)ControllerButton.SL])
                {
                    Simulate(settings.LeftJoyconL, false, true);
                }

                if (buttons_down[(int)ControllerButton.SR])
                {
                    Simulate(settings.LeftJoyconR, false, false);
                }

                if (buttons_up[(int)ControllerButton.SR])
                {
                    Simulate(settings.LeftJoyconR, false, true);
                }

                SimulateContinous((int)ControllerButton.SL, settings.LeftJoyconL);
                SimulateContinous((int)ControllerButton.SR, settings.LeftJoyconR);
            }
            else
            {
                if (buttons_down[(int)ControllerButton.SL])
                {
                    Simulate(settings.RightJoyconL, false, false);
                }

                if (buttons_up[(int)ControllerButton.SL])
                {
                    Simulate(settings.RightJoyconL, false, true);
                }

                if (buttons_down[(int)ControllerButton.SR])
                {
                    Simulate(settings.RightJoyconR, false, false);
                }

                if (buttons_up[(int)ControllerButton.SR])
                {
                    Simulate(settings.RightJoyconR, false, true);
                }

                SimulateContinous((int)ControllerButton.SL, settings.RightJoyconL);
                SimulateContinous((int)ControllerButton.SR, settings.RightJoyconR);
            }

            // Filtered IMU data
            cur_rotation = gyroHelper.GetEulerAngles();
            float dt = 0.015f; // 15ms

            if (settings.GyroAnalogSliders && (Other != null || Type == ControllerType.PRO_CONTROLLER))
            {
                ControllerButton leftT = Type == ControllerType.LEFT_JOYCON ? ControllerButton.SHOULDER_2 : ControllerButton.SHOULDER2_2;
                ControllerButton rightT = Type == ControllerType.LEFT_JOYCON ? ControllerButton.SHOULDER2_2 : ControllerButton.SHOULDER_2;

                Joycon left = Type == ControllerType.LEFT_JOYCON ? this : (Type == ControllerType.PRO_CONTROLLER ? this : Other);
                Joycon right = Type != ControllerType.LEFT_JOYCON ? this : (Type == ControllerType.PRO_CONTROLLER ? this : Other);

                int ldy, rdy;
                if (settings.UseFilteredIMU)
                {
                    ldy = (int)(settings.GyroAnalogSensitivity * (left.cur_rotation[0] - left.cur_rotation[3]));
                    rdy = (int)(settings.GyroAnalogSensitivity * (right.cur_rotation[0] - right.cur_rotation[3]));
                }
                else
                {
                    ldy = (int)(settings.GyroAnalogSensitivity * (left.gyr_g.Y * dt));
                    rdy = (int)(settings.GyroAnalogSensitivity * (right.gyr_g.Y * dt));
                }

                if (buttons[(int)leftT])
                {
                    sliderVal[0] = (byte)Math.Min(byte.MaxValue, Math.Max(0, sliderVal[0] + ldy));
                }
                else
                {
                    sliderVal[0] = 0;
                }

                if (buttons[(int)rightT])
                {
                    sliderVal[1] = (byte)Math.Min(byte.MaxValue, Math.Max(0, sliderVal[1] + rdy));
                }
                else
                {
                    sliderVal[1] = 0;
                }
            }

            string res_val = settings.ActiveGyro;
            if (res_val.StartsWith("joy_"))
            {
                var i = int.Parse(res_val.Substring(4));
                if (settings.GyroHoldToggle)
                {
                    if (buttons_down[i] || (Other != null && Other.buttons_down[i]))
                    {
                        active_gyro = true;
                    }
                    else if (buttons_up[i] || (Other != null && Other.buttons_up[i]))
                    {
                        active_gyro = false;
                    }
                }
                else
                {
                    if (buttons_down[i] || (Other != null && Other.buttons_down[i]))
                    {
                        active_gyro = !active_gyro;
                    }
                }
            }

            if (settings.GyroToJoyOrMouse[..3] == "joy")
            {
                if (settings.ActiveGyro == "0" || active_gyro)
                {
                    float[] control_stick = (settings.GyroToJoyOrMouse == "joy_left") ? stick : stick2;

                    float dx, dy;
                    if (settings.UseFilteredIMU)
                    {
                        dx = (settings.GyroStickSensitivityX * (cur_rotation[1] - cur_rotation[4])); // yaw
                        dy = -(settings.GyroStickSensitivityY * (cur_rotation[0] - cur_rotation[3])); // pitch
                    }
                    else
                    {
                        dx = (settings.GyroStickSensitivityX * (gyr_g.Z * dt)); // yaw
                        dy = -(settings.GyroStickSensitivityY * (gyr_g.Y * dt)); // pitch
                    }

                    control_stick[0] = Math.Max(-1.0f, Math.Min(1.0f, control_stick[0] / settings.GyroStickReduction + dx));
                    control_stick[1] = Math.Max(-1.0f, Math.Min(1.0f, control_stick[1] / settings.GyroStickReduction + dy));
                }
            }
            //TODO: probably gonna throw this away if my assumption is correct
            //that it's just for controlling gyro with mouse
            //else if (settings.GyroToJoyOrMouse == "mouse" && (controllerType == ControllerType.PRO_CONTROLLER || (Other == null) || (Other != null && (settings.GyroMouseLeftHanded ? controllerType == ControllerType.LEFT_JOYCON : controllerType != ControllerType.LEFT_JOYCON))))
            //{
            //    // gyro data is in degrees/s
            //    if (settings.ActiveGyro == "0" || active_gyro)
            //    {
            //        int dx, dy;

            //        if (settings.UseFilteredIMU)
            //        {
            //            dx = (int)(settings.GyroMouseSensitivityX * (cur_rotation[1] - cur_rotation[4])); // yaw
            //            dy = (int)-(settings.GyroMouseSensitivityY * (cur_rotation[0] - cur_rotation[3])); // pitch
            //        }
            //        else
            //        {
            //            dx = (int)(settings.GyroMouseSensitivityX * (gyr_g.Z * dt));
            //            dy = (int)-(settings.GyroMouseSensitivityY * (gyr_g.Y * dt));
            //        }

            //        //robot.MouseMove(dx, dy);
            //        WindowsInput.Simulate.Events().MoveBy(dx, dy).Invoke();
            //    }

            //    // reset mouse position to centre of primary monitor
            //    res_val = settings.ResetMouse;
            //    if (res_val.StartsWith("joy_"))
            //    {
            //        int i = int.Parse(res_val[4..]);
            //        if (buttons_down[i] || (Other != null && Other.buttons_down[i]))
            //        {
            //            WindowsInput.Simulate.Events().MoveTo(Screen.PrimaryScreen.Bounds.Width / 2, Screen.PrimaryScreen.Bounds.Height / 2).Invoke();
            //        }
            //    }
            //}
        }

        public Task Begin(CancellationToken? cancellationToken = null)
        {
            return Task.Factory.StartNew(() =>
            {
                logger.LogInformation($"Started listening to {serialNumber}.");

                var attempts = 0;
                while (true)
                {
                    if (cancellationToken?.IsCancellationRequested ?? false)
                    {
                        logger.LogInformation($"Stopped listening to {serialNumber}.");
                        return;
                    }

                    if (attempts > 240)
                    {
                        State = ControllerState.DROPPED;
                        logger.LogInformation($"Dropped joycon {serialNumber}.");
                        return;
                    }

                    //TODO: this rumble logic should be handled in the same function below
                    if (rumble.queue.Count > 0)
                    {
                        SendRumble(rumble.GetData());
                    }

                    attempts = ReceiveRaw() ? 0 : attempts + 1;
                }
            }, cancellationToken ?? CancellationToken.None, TaskCreationOptions.LongRunning, TaskScheduler.Default);
        }

        public float[] otherStick = { 0, 0 };
        private int ProcessButtonsAndStick(byte[] report_buf)
        {
            if (report_buf[0] == 0x00)
            {
                throw new ArgumentException("received undefined report. This is probably a bug");
            }

            if (Type != ControllerType.SNES_CONTROLLER)
            {
                stick_raw[0] = report_buf[6 + (Type == ControllerType.LEFT_JOYCON ? 0 : 3)];
                stick_raw[1] = report_buf[7 + (Type == ControllerType.LEFT_JOYCON ? 0 : 3)];
                stick_raw[2] = report_buf[8 + (Type == ControllerType.LEFT_JOYCON ? 0 : 3)];

                if (Type == ControllerType.PRO_CONTROLLER)
                {
                    stick2_raw[0] = report_buf[6 + (Type != ControllerType.LEFT_JOYCON ? 0 : 3)];
                    stick2_raw[1] = report_buf[7 + (Type != ControllerType.LEFT_JOYCON ? 0 : 3)];
                    stick2_raw[2] = report_buf[8 + (Type != ControllerType.LEFT_JOYCON ? 0 : 3)];
                }

                stick_precal[0] = (ushort)(stick_raw[0] | ((stick_raw[1] & 0xf) << 8));
                stick_precal[1] = (ushort)((stick_raw[1] >> 4) | (stick_raw[2] << 4));
                stick = CalculateStickData(stick_precal, stick_cal, deadzone);

                if (Type == ControllerType.PRO_CONTROLLER)
                {
                    stick2_precal[0] = (ushort)(stick2_raw[0] | ((stick2_raw[1] & 0xf) << 8));
                    stick2_precal[1] = (ushort)((stick2_raw[1] >> 4) | (stick2_raw[2] << 4));
                    stick2 = CalculateStickData(stick2_precal, stick2_cal, deadzone2);
                }

                // Read other Joycon's sticks
                if (Type == ControllerType.LEFT_JOYCON && Other != null && Other != this)
                {
                    stick2 = otherStick;
                    Other.otherStick = stick;
                }

                if (Type != ControllerType.LEFT_JOYCON && Other != null && Other != this)
                {
                    Array.Copy(stick, stick2, 2);
                    stick = otherStick;
                    Other.otherStick = stick2;
                }
            }

            // Set button states both for server and ViGEm
            lock (buttons)
            {
                lock (down_)
                {
                    for (int i = 0; i < buttons.Length; ++i)
                    {
                        down_[i] = buttons[i];
                    }
                }
                buttons = new bool[20];

                //TODO: convert all this to list of enums instead of this crap array of casted enums
                buttons[(int)ControllerButton.DPAD_DOWN] = (report_buf[3 + (Type == ControllerType.LEFT_JOYCON ? 2 : 0)] & (Type == ControllerType.LEFT_JOYCON ? 0x01 : 0x04)) != 0;
                buttons[(int)ControllerButton.DPAD_RIGHT] = (report_buf[3 + (Type == ControllerType.LEFT_JOYCON ? 2 : 0)] & (Type == ControllerType.LEFT_JOYCON ? 0x04 : 0x08)) != 0;
                buttons[(int)ControllerButton.DPAD_UP] = (report_buf[3 + (Type == ControllerType.LEFT_JOYCON ? 2 : 0)] & (Type == ControllerType.LEFT_JOYCON ? 0x02 : 0x02)) != 0;
                buttons[(int)ControllerButton.DPAD_LEFT] = (report_buf[3 + (Type == ControllerType.LEFT_JOYCON ? 2 : 0)] & (Type == ControllerType.LEFT_JOYCON ? 0x08 : 0x01)) != 0;
                buttons[(int)ControllerButton.HOME] = ((report_buf[4] & 0x10) != 0);
                buttons[(int)ControllerButton.CAPTURE] = ((report_buf[4] & 0x20) != 0);
                buttons[(int)ControllerButton.MINUS] = ((report_buf[4] & 0x01) != 0);
                buttons[(int)ControllerButton.PLUS] = ((report_buf[4] & 0x02) != 0);
                buttons[(int)ControllerButton.STICK] = ((report_buf[4] & (Type == ControllerType.LEFT_JOYCON ? 0x08 : 0x04)) != 0);
                buttons[(int)ControllerButton.SHOULDER_1] = (report_buf[3 + (Type == ControllerType.LEFT_JOYCON ? 2 : 0)] & 0x40) != 0;
                buttons[(int)ControllerButton.SHOULDER_2] = (report_buf[3 + (Type == ControllerType.LEFT_JOYCON ? 2 : 0)] & 0x80) != 0;
                buttons[(int)ControllerButton.SR] = (report_buf[3 + (Type == ControllerType.LEFT_JOYCON ? 2 : 0)] & 0x10) != 0;
                buttons[(int)ControllerButton.SL] = (report_buf[3 + (Type == ControllerType.LEFT_JOYCON ? 2 : 0)] & 0x20) != 0;

                if (Type == ControllerType.PRO_CONTROLLER)
                {
                    buttons[(int)ControllerButton.B] = (report_buf[3 + (Type != ControllerType.LEFT_JOYCON ? 2 : 0)] & (Type != ControllerType.LEFT_JOYCON ? 0x01 : 0x04)) != 0;
                    buttons[(int)ControllerButton.A] = (report_buf[3 + (Type != ControllerType.LEFT_JOYCON ? 2 : 0)] & (Type != ControllerType.LEFT_JOYCON ? 0x04 : 0x08)) != 0;
                    buttons[(int)ControllerButton.X] = (report_buf[3 + (Type != ControllerType.LEFT_JOYCON ? 2 : 0)] & (Type != ControllerType.LEFT_JOYCON ? 0x02 : 0x02)) != 0;
                    buttons[(int)ControllerButton.Y] = (report_buf[3 + (Type != ControllerType.LEFT_JOYCON ? 2 : 0)] & (Type != ControllerType.LEFT_JOYCON ? 0x08 : 0x01)) != 0;

                    buttons[(int)ControllerButton.STICK2] = ((report_buf[4] & (Type != ControllerType.LEFT_JOYCON ? 0x08 : 0x04)) != 0);
                    buttons[(int)ControllerButton.SHOULDER2_1] = (report_buf[3 + (Type != ControllerType.LEFT_JOYCON ? 2 : 0)] & 0x40) != 0;
                    buttons[(int)ControllerButton.SHOULDER2_2] = (report_buf[3 + (Type != ControllerType.LEFT_JOYCON ? 2 : 0)] & 0x80) != 0;
                }

                if (Other != null && Other != this)
                {
                    buttons[(int)(ControllerButton.B)] = Other.buttons[(int)ControllerButton.DPAD_DOWN];
                    buttons[(int)(ControllerButton.A)] = Other.buttons[(int)ControllerButton.DPAD_RIGHT];
                    buttons[(int)(ControllerButton.X)] = Other.buttons[(int)ControllerButton.DPAD_UP];
                    buttons[(int)(ControllerButton.Y)] = Other.buttons[(int)ControllerButton.DPAD_LEFT];

                    buttons[(int)ControllerButton.STICK2] = Other.buttons[(int)ControllerButton.STICK];
                    buttons[(int)ControllerButton.SHOULDER2_1] = Other.buttons[(int)ControllerButton.SHOULDER_1];
                    buttons[(int)ControllerButton.SHOULDER2_2] = Other.buttons[(int)ControllerButton.SHOULDER_2];
                }

                if (Type == ControllerType.LEFT_JOYCON && Other != null && Other != this)
                {
                    buttons[(int)ControllerButton.HOME] = Other.buttons[(int)ControllerButton.HOME];
                    buttons[(int)ControllerButton.PLUS] = Other.buttons[(int)ControllerButton.PLUS];
                }

                if (Type != ControllerType.LEFT_JOYCON && Other != null && Other != this)
                {
                    buttons[(int)ControllerButton.MINUS] = Other.buttons[(int)ControllerButton.MINUS];
                }

                long timestamp = Stopwatch.GetTimestamp();

                lock (buttons_up)
                {
                    lock (buttons_down)
                    {
                        bool changed = false;
                        for (int i = 0; i < buttons.Length; ++i)
                        {
                            buttons_up[i] = down_[i] & !buttons[i];
                            buttons_down[i] = !down_[i] & buttons[i];

                            if (down_[i] != buttons[i])
                            {
                                buttons_down_timestamp[i] = buttons[i] ? timestamp : -1;
                            }

                            if (buttons_up[i] || buttons_down[i])
                            {
                                changed = true;
                            }
                        }

                        inactivity = changed ? timestamp : inactivity;
                    }
                }
            }

            return 0;
        }

        // Get Gyro/Accel data
        private void ExtractIMUValues(byte[] report_buf, int n = 0)
        {
            if (Type != ControllerType.SNES_CONTROLLER)
            {
                gyr_r[0] = (short)(report_buf[19 + n * 12] | ((report_buf[20 + n * 12] << 8) & 0xff00));
                gyr_r[1] = (short)(report_buf[21 + n * 12] | ((report_buf[22 + n * 12] << 8) & 0xff00));
                gyr_r[2] = (short)(report_buf[23 + n * 12] | ((report_buf[24 + n * 12] << 8) & 0xff00));
                acc_r[0] = (short)(report_buf[13 + n * 12] | ((report_buf[14 + n * 12] << 8) & 0xff00));
                acc_r[1] = (short)(report_buf[15 + n * 12] | ((report_buf[16 + n * 12] << 8) & 0xff00));
                acc_r[2] = (short)(report_buf[17 + n * 12] | ((report_buf[18 + n * 12] << 8) & 0xff00));

                //TODO: figure out where to put user input for cal data
                if (false) //(form.allowCalibration)
                {
                    //for (int i = 0; i < 3; ++i)
                    //{
                    //    switch (i)
                    //    {
                    //        case 0:
                    //            acc_g.X = (acc_r[i] - activeData[3]) * (1.0f / acc_sen[i]) * 4.0f;
                    //            gyr_g.X = (gyr_r[i] - activeData[0]) * (816.0f / gyr_sen[i]);
                    //            if (form.calibrate)
                    //            {
                    //                form.xA.Add(acc_r[i]);
                    //                form.xG.Add(gyr_r[i]);
                    //            }
                    //            break;
                    //        case 1:
                    //            acc_g.Y = (controllerType != ControllerType.LEFT_JOYCON ? -1 : 1) * (acc_r[i] - activeData[4]) * (1.0f / acc_sen[i]) * 4.0f;
                    //            gyr_g.Y = -(controllerType != ControllerType.LEFT_JOYCON ? -1 : 1) * (gyr_r[i] - activeData[1]) * (816.0f / gyr_sen[i]);
                    //            if (form.calibrate)
                    //            {
                    //                form.yA.Add(acc_r[i]);
                    //                form.yG.Add(gyr_r[i]);
                    //            }
                    //            break;
                    //        case 2:
                    //            acc_g.Z = (controllerType != ControllerType.LEFT_JOYCON ? -1 : 1) * (acc_r[i] - activeData[5]) * (1.0f / acc_sen[i]) * 4.0f;
                    //            gyr_g.Z = -(controllerType != ControllerType.LEFT_JOYCON ? -1 : 1) * (gyr_r[i] - activeData[2]) * (816.0f / gyr_sen[i]);
                    //            if (form.calibrate)
                    //            {
                    //                form.zA.Add(acc_r[i]);
                    //                form.zG.Add(gyr_r[i]);
                    //            }
                    //            break;
                    //    }
                    //}
                }
                else
                {
                    short[] offset;
                    if (Type == ControllerType.PRO_CONTROLLER)
                    {
                        offset = pro_hor_offset;
                    }
                    else if (Type == ControllerType.LEFT_JOYCON)
                    {
                        offset = left_hor_offset;
                    }
                    else
                    {
                        offset = right_hor_offset;
                    }

                    for (var i = 0; i < 3; ++i)
                    {
                        switch (i)
                        {
                            case 0:
                                acc_g.X = (acc_r[i] - offset[i]) * (1.0f / (acc_sensiti[i] - acc_neutral[i])) * 4.0f;
                                gyr_g.X = (gyr_r[i] - gyr_neutral[i]) * (816.0f / (gyr_sensiti[i] - gyr_neutral[i]));
                                break;
                            case 1:
                                acc_g.Y = (Type != ControllerType.LEFT_JOYCON ? -1 : 1) * (acc_r[i] - offset[i]) * (1.0f / (acc_sensiti[i] - acc_neutral[i])) * 4.0f;
                                gyr_g.Y = -(Type != ControllerType.LEFT_JOYCON ? -1 : 1) * (gyr_r[i] - gyr_neutral[i]) * (816.0f / (gyr_sensiti[i] - gyr_neutral[i]));
                                break;
                            case 2:
                                acc_g.Z = (Type != ControllerType.LEFT_JOYCON ? -1 : 1) * (acc_r[i] - offset[i]) * (1.0f / (acc_sensiti[i] - acc_neutral[i])) * 4.0f;
                                gyr_g.Z = -(Type != ControllerType.LEFT_JOYCON ? -1 : 1) * (gyr_r[i] - gyr_neutral[i]) * (816.0f / (gyr_sensiti[i] - gyr_neutral[i]));
                                break;
                        }
                    }
                }

                if (Other == null && Type != ControllerType.PRO_CONTROLLER)
                {
                    // single joycon mode; Z do not swap, rest do
                    if (Type == ControllerType.LEFT_JOYCON)
                    {
                        acc_g.X = -acc_g.X;
                        acc_g.Y = -acc_g.Y;
                        gyr_g.X = -gyr_g.X;
                    }
                    else
                    {
                        gyr_g.Y = -gyr_g.Y;
                    }

                    var temp = acc_g.X;
                    acc_g.X = acc_g.Y;
                    acc_g.Y = -temp;

                    temp = gyr_g.X;
                    gyr_g.X = gyr_g.Y;
                    gyr_g.Y = temp;
                }

                // Update rotation Quaternion
                var deg_to_rad = 0.0174533f;
                gyroHelper.Update(gyr_g.X * deg_to_rad, gyr_g.Y * deg_to_rad, gyr_g.Z * deg_to_rad, acc_g.X, acc_g.Y, acc_g.Z);
            }
        }

        private static float[] CalculateStickData(ushort[] vals, ushort[] cal, ushort dz)
        {
            ushort[] t = cal;

            float[] s = { 0, 0 };
            float dx = vals[0] - t[2], dy = vals[1] - t[3];
            if (Math.Abs(dx * dx + dy * dy) < dz * dz)
            {
                return s;
            }

            s[0] = dx / (dx > 0 ? t[0] : t[4]);
            s[1] = dy / (dy > 0 ? t[1] : t[5]);

            return s;
        }

        //TODO: combines these into T generic return probably using a switch expression
        private static short CastStickValue(float stick_value)
        {
            return (short)Math.Max(short.MinValue, Math.Min(short.MaxValue, stick_value * (stick_value > 0 ? short.MaxValue : -short.MinValue)));
        }

        private static byte CastStickValueByte(float stick_value)
        {
            return (byte)Math.Max(byte.MinValue, Math.Min(byte.MaxValue, 127 - stick_value * byte.MaxValue));
        }

        private void SetRumble(float low_freq, float high_freq, float amp)
        {
            if (State <= ControllerState.ATTACHED)
            {
                return;
            }

            rumble.SetVals(low_freq, high_freq, amp);
        }

        private void SendRumble(byte[] buf)
        {
            var buf_ = new byte[Constants.REPORT_LENGTH];
            buf_[0] = 0x10;
            buf_[1] = global_count;

            if (global_count == 0xf)
            {
                global_count = 0;
            }
            else
            {
                global_count++;
            }

            Array.Copy(buf, 0, buf_, 2, 8);
            hidService.Write(Handle, buf_);
        }

        private byte[] Subcommand(byte sc, byte[] buf, int len)
        {
            byte[] buf_ = new byte[Constants.REPORT_LENGTH];
            Array.Copy(default_buf, 0, buf_, 2, 8);
            Array.Copy(buf, 0, buf_, 11, len);
            buf_[10] = sc;
            buf_[1] = global_count;
            buf_[0] = 0x1;

            if (global_count == 0xf)
            {
                global_count = 0;
            }
            else
            {
                global_count++;
            }

            //TODO: I don't like this +11 hardcoded, but can it be calculated?
            hidService.Write(Handle, buf_, len + 11);

            //TODO: does this really need to be a do while?
            byte[] data;
            var tries = 0;
            do
            {
                data = hidService.Read(Handle, 100);
                tries++;
            } while (tries < 10 && data[0] != 0x21 && data[14] != sc);

            return data;
        }

        private void LoadCalibrationData()
        {
            if (Type == ControllerType.SNES_CONTROLLER)
            {
                //TODO: get rid of this string parsing crap
                short[] temp = settings.acc_sensiti.Split(',').Select(s => short.Parse(s)).ToArray();
                acc_sensiti[0] = temp[0];
                acc_sensiti[1] = temp[1];
                acc_sensiti[2] = temp[2];

                temp = settings.gyr_sensiti.Split(',').Select(s => short.Parse(s)).ToArray();
                gyr_sensiti[0] = temp[0];
                gyr_sensiti[1] = temp[1];
                gyr_sensiti[2] = temp[2];

                ushort[] temp2 = settings.stick_cal.Split(',').Select(s => ushort.Parse(s[2..], NumberStyles.HexNumber)).ToArray();
                stick_cal[0] = temp2[0];
                stick_cal[1] = temp2[1];
                stick_cal[2] = temp2[2];
                stick_cal[3] = temp2[3];
                stick_cal[4] = temp2[4];
                stick_cal[5] = temp2[5];

                temp2 = settings.stick2_cal.Split(',').Select(s => ushort.Parse(s[2..], NumberStyles.HexNumber)).ToArray();
                stick2_cal[0] = temp2[0];
                stick2_cal[1] = temp2[1];
                stick2_cal[2] = temp2[2];
                stick2_cal[3] = temp2[3];
                stick2_cal[4] = temp2[4];
                stick2_cal[5] = temp2[5];

                deadzone = settings.deadzone;
                deadzone2 = settings.deadzone2;

                return;
            }

            hidService.SetDeviceNonblocking(Handle, false);

            byte[] buf_ = ReadSPI(0x80, Type == ControllerType.LEFT_JOYCON ? (byte)0x12 : (byte)0x1d, 9);
            bool found = false;
            for (int i = 0; i < 9; ++i)
            {
                if (buf_[i] != 0xff)
                {
                    logger.LogInformation("Using user stick calibration data.");
                    found = true;
                    break;
                }
            }

            if (!found)
            {
                logger.LogInformation("Using factory stick calibration data.");
                buf_ = ReadSPI(0x60, Type == ControllerType.LEFT_JOYCON ? (byte)0x3d : (byte)0x46, 9);
            }

            stick_cal[Type == ControllerType.LEFT_JOYCON ? 0 : 2] = (ushort)((buf_[1] << 8) & 0xF00 | buf_[0]); // X Axis Max above center
            stick_cal[Type == ControllerType.LEFT_JOYCON ? 1 : 3] = (ushort)((buf_[2] << 4) | (buf_[1] >> 4));  // Y Axis Max above center
            stick_cal[Type == ControllerType.LEFT_JOYCON ? 2 : 4] = (ushort)((buf_[4] << 8) & 0xF00 | buf_[3]); // X Axis Center
            stick_cal[Type == ControllerType.LEFT_JOYCON ? 3 : 5] = (ushort)((buf_[5] << 4) | (buf_[4] >> 4));  // Y Axis Center
            stick_cal[Type == ControllerType.LEFT_JOYCON ? 4 : 0] = (ushort)((buf_[7] << 8) & 0xF00 | buf_[6]); // X Axis Min below center
            stick_cal[Type == ControllerType.LEFT_JOYCON ? 5 : 1] = (ushort)((buf_[8] << 4) | (buf_[7] >> 4));  // Y Axis Min below center

            if (Type == ControllerType.PRO_CONTROLLER)
            {
                buf_ = ReadSPI(0x80, Type != ControllerType.LEFT_JOYCON ? (byte)0x12 : (byte)0x1d, 9);
                found = false;
                for (int i = 0; i < 9; ++i)
                {
                    if (buf_[i] != 0xff)
                    {
                        logger.LogInformation("Using user stick calibration data.");
                        found = true;
                        break;
                    }
                }

                if (!found)
                {
                    logger.LogInformation("Using factory stick calibration data.");
                    buf_ = ReadSPI(0x60, (Type != ControllerType.LEFT_JOYCON ? (byte)0x3d : (byte)0x46), 9);
                }

                stick2_cal[Type != ControllerType.LEFT_JOYCON ? 0 : 2] = (ushort)((buf_[1] << 8) & 0xF00 | buf_[0]); // X Axis Max above center
                stick2_cal[Type != ControllerType.LEFT_JOYCON ? 1 : 3] = (ushort)((buf_[2] << 4) | (buf_[1] >> 4));  // Y Axis Max above center
                stick2_cal[Type != ControllerType.LEFT_JOYCON ? 2 : 4] = (ushort)((buf_[4] << 8) & 0xF00 | buf_[3]); // X Axis Center
                stick2_cal[Type != ControllerType.LEFT_JOYCON ? 3 : 5] = (ushort)((buf_[5] << 4) | (buf_[4] >> 4));  // Y Axis Center
                stick2_cal[Type != ControllerType.LEFT_JOYCON ? 4 : 0] = (ushort)((buf_[7] << 8) & 0xF00 | buf_[6]); // X Axis Min below center
                stick2_cal[Type != ControllerType.LEFT_JOYCON ? 5 : 1] = (ushort)((buf_[8] << 4) | (buf_[7] >> 4));  // Y Axis Min below center

                buf_ = ReadSPI(0x60, Type != ControllerType.LEFT_JOYCON ? (byte)0x86 : (byte)0x98, 16);
                deadzone2 = (ushort)((buf_[4] << 8) & 0xF00 | buf_[3]);
            }

            buf_ = ReadSPI(0x60, Type == ControllerType.LEFT_JOYCON ? (byte)0x86 : (byte)0x98, 16);
            deadzone = (ushort)((buf_[4] << 8) & 0xF00 | buf_[3]);

            buf_ = ReadSPI(0x80, 0x28, 10);
            acc_neutral[0] = (short)(buf_[0] | ((buf_[1] << 8) & 0xff00));
            acc_neutral[1] = (short)(buf_[2] | ((buf_[3] << 8) & 0xff00));
            acc_neutral[2] = (short)(buf_[4] | ((buf_[5] << 8) & 0xff00));

            buf_ = ReadSPI(0x80, 0x2E, 10);
            acc_sensiti[0] = (short)(buf_[0] | ((buf_[1] << 8) & 0xff00));
            acc_sensiti[1] = (short)(buf_[2] | ((buf_[3] << 8) & 0xff00));
            acc_sensiti[2] = (short)(buf_[4] | ((buf_[5] << 8) & 0xff00));

            buf_ = ReadSPI(0x80, 0x34, 10);
            gyr_neutral[0] = (short)(buf_[0] | ((buf_[1] << 8) & 0xff00));
            gyr_neutral[1] = (short)(buf_[2] | ((buf_[3] << 8) & 0xff00));
            gyr_neutral[2] = (short)(buf_[4] | ((buf_[5] << 8) & 0xff00));

            buf_ = ReadSPI(0x80, 0x3A, 10);
            gyr_sensiti[0] = (short)(buf_[0] | ((buf_[1] << 8) & 0xff00));
            gyr_sensiti[1] = (short)(buf_[2] | ((buf_[3] << 8) & 0xff00));
            gyr_sensiti[2] = (short)(buf_[4] | ((buf_[5] << 8) & 0xff00));

            // This is an extremely messy way of checking to see whether there is user stick calibration data present, but I've seen conflicting user calibration data on blank Joy-Cons. Worth another look eventually.
            if (gyr_neutral[0] + gyr_neutral[1] + gyr_neutral[2] == -3 || Math.Abs(gyr_neutral[0]) > 100 || Math.Abs(gyr_neutral[1]) > 100 || Math.Abs(gyr_neutral[2]) > 100)
            {
                buf_ = ReadSPI(0x60, 0x20, 10);
                acc_neutral[0] = (short)(buf_[0] | ((buf_[1] << 8) & 0xff00));
                acc_neutral[1] = (short)(buf_[2] | ((buf_[3] << 8) & 0xff00));
                acc_neutral[2] = (short)(buf_[4] | ((buf_[5] << 8) & 0xff00));

                buf_ = ReadSPI(0x60, 0x26, 10);
                acc_sensiti[0] = (short)(buf_[0] | ((buf_[1] << 8) & 0xff00));
                acc_sensiti[1] = (short)(buf_[2] | ((buf_[3] << 8) & 0xff00));
                acc_sensiti[2] = (short)(buf_[4] | ((buf_[5] << 8) & 0xff00));

                buf_ = ReadSPI(0x60, 0x2C, 10);
                gyr_neutral[0] = (short)(buf_[0] | ((buf_[1] << 8) & 0xff00));
                gyr_neutral[1] = (short)(buf_[2] | ((buf_[3] << 8) & 0xff00));
                gyr_neutral[2] = (short)(buf_[4] | ((buf_[5] << 8) & 0xff00));

                buf_ = ReadSPI(0x60, 0x32, 10);
                gyr_sensiti[0] = (short)(buf_[0] | ((buf_[1] << 8) & 0xff00));
                gyr_sensiti[1] = (short)(buf_[2] | ((buf_[3] << 8) & 0xff00));
                gyr_sensiti[2] = (short)(buf_[4] | ((buf_[5] << 8) & 0xff00));
            }

            hidService.SetDeviceNonblocking(Handle);
        }

        private byte[] ReadSPI(byte addr1, byte addr2, uint len)
        {
            byte[] buf = { addr2, addr1, 0x00, 0x00, (byte)len };
            byte[] read_buf = new byte[len];
            byte[] buf_ = new byte[len + 20];

            for (int i = 0; i < 100; ++i)
            {
                buf_ = Subcommand(0x10, buf, 5);
                if (buf_[15] == addr2 && buf_[16] == addr1)
                {
                    break;
                }
            }

            Array.Copy(buf_, 20, read_buf, 0, len);

            return read_buf;
        }

        private VirtualControllerState MapToVirtualControllerInput(Joycon input)
        {
            var output = new VirtualControllerState();
            var other = input.Other;

            var buttons = input.buttons;
            var stick = input.stick;
            var stick2 = input.stick2;
            var sliderVal = input.sliderVal;

            if (Type == ControllerType.PRO_CONTROLLER)
            {
                output.a = buttons[(int)(!settings.SwapAB ? ControllerButton.B : ControllerButton.A)];
                output.b = buttons[(int)(!settings.SwapAB ? ControllerButton.A : ControllerButton.B)];
                output.y = buttons[(int)(!settings.SwapXY ? ControllerButton.X : ControllerButton.Y)];
                output.x = buttons[(int)(!settings.SwapXY ? ControllerButton.Y : ControllerButton.X)];

                output.dpad_up = buttons[(int)ControllerButton.DPAD_UP];
                output.dpad_down = buttons[(int)ControllerButton.DPAD_DOWN];
                output.dpad_left = buttons[(int)ControllerButton.DPAD_LEFT];
                output.dpad_right = buttons[(int)ControllerButton.DPAD_RIGHT];

                output.back = buttons[(int)ControllerButton.MINUS];
                output.start = buttons[(int)ControllerButton.PLUS];
                output.guide = buttons[(int)ControllerButton.HOME];

                output.shoulder_left = buttons[(int)ControllerButton.SHOULDER_1];
                output.shoulder_right = buttons[(int)ControllerButton.SHOULDER2_1];

                output.thumb_stick_left = buttons[(int)ControllerButton.STICK];
                output.thumb_stick_right = buttons[(int)ControllerButton.STICK2];
            }
            else
            {
                if (other != null)
                {
                    // no need for && other != this
                    output.a = buttons[(int)(!settings.SwapAB ? Type == ControllerType.LEFT_JOYCON ? ControllerButton.B : ControllerButton.DPAD_DOWN : Type == ControllerType.LEFT_JOYCON ? ControllerButton.A : ControllerButton.DPAD_RIGHT)];
                    output.b = buttons[(int)(settings.SwapAB ? Type == ControllerType.LEFT_JOYCON ? ControllerButton.B : ControllerButton.DPAD_DOWN : Type == ControllerType.LEFT_JOYCON ? ControllerButton.A : ControllerButton.DPAD_RIGHT)];
                    output.y = buttons[(int)(!settings.SwapXY ? Type == ControllerType.LEFT_JOYCON ? ControllerButton.X : ControllerButton.DPAD_UP : Type == ControllerType.LEFT_JOYCON ? ControllerButton.Y : ControllerButton.DPAD_LEFT)];
                    output.x = buttons[(int)(settings.SwapXY ? Type == ControllerType.LEFT_JOYCON ? ControllerButton.X : ControllerButton.DPAD_UP : Type == ControllerType.LEFT_JOYCON ? ControllerButton.Y : ControllerButton.DPAD_LEFT)];

                    output.dpad_up = buttons[(int)(Type == ControllerType.LEFT_JOYCON ? ControllerButton.DPAD_UP : ControllerButton.X)];
                    output.dpad_down = buttons[(int)(Type == ControllerType.LEFT_JOYCON ? ControllerButton.DPAD_DOWN : ControllerButton.B)];
                    output.dpad_left = buttons[(int)(Type == ControllerType.LEFT_JOYCON ? ControllerButton.DPAD_LEFT : ControllerButton.Y)];
                    output.dpad_right = buttons[(int)(Type == ControllerType.LEFT_JOYCON ? ControllerButton.DPAD_RIGHT : ControllerButton.A)];

                    output.back = buttons[(int)ControllerButton.MINUS];
                    output.start = buttons[(int)ControllerButton.PLUS];
                    output.guide = buttons[(int)ControllerButton.HOME];

                    output.shoulder_left = buttons[(int)(Type == ControllerType.LEFT_JOYCON ? ControllerButton.SHOULDER_1 : ControllerButton.SHOULDER2_1)];
                    output.shoulder_right = buttons[(int)(Type == ControllerType.LEFT_JOYCON ? ControllerButton.SHOULDER2_1 : ControllerButton.SHOULDER_1)];

                    output.thumb_stick_left = buttons[(int)(Type == ControllerType.LEFT_JOYCON ? ControllerButton.STICK : ControllerButton.STICK2)];
                    output.thumb_stick_right = buttons[(int)(Type == ControllerType.LEFT_JOYCON ? ControllerButton.STICK2 : ControllerButton.STICK)];
                }
                else
                {
                    // single joycon mode
                    output.a = buttons[(int)(!settings.SwapAB ? Type == ControllerType.LEFT_JOYCON ? ControllerButton.DPAD_LEFT : ControllerButton.DPAD_RIGHT : Type == ControllerType.LEFT_JOYCON ? ControllerButton.DPAD_DOWN : ControllerButton.DPAD_UP)];
                    output.b = buttons[(int)(settings.SwapAB ? Type == ControllerType.LEFT_JOYCON ? ControllerButton.DPAD_LEFT : ControllerButton.DPAD_RIGHT : Type == ControllerType.LEFT_JOYCON ? ControllerButton.DPAD_DOWN : ControllerButton.DPAD_UP)];
                    output.y = buttons[(int)(!settings.SwapXY ? Type == ControllerType.LEFT_JOYCON ? ControllerButton.DPAD_RIGHT : ControllerButton.DPAD_LEFT : Type == ControllerType.LEFT_JOYCON ? ControllerButton.DPAD_UP : ControllerButton.DPAD_DOWN)];
                    output.x = buttons[(int)(settings.SwapXY ? Type == ControllerType.LEFT_JOYCON ? ControllerButton.DPAD_RIGHT : ControllerButton.DPAD_LEFT : Type == ControllerType.LEFT_JOYCON ? ControllerButton.DPAD_UP : ControllerButton.DPAD_DOWN)];

                    output.back = buttons[(int)ControllerButton.MINUS] | buttons[(int)ControllerButton.HOME];
                    output.start = buttons[(int)ControllerButton.PLUS] | buttons[(int)ControllerButton.CAPTURE];

                    output.shoulder_left = buttons[(int)ControllerButton.SL];
                    output.shoulder_right = buttons[(int)ControllerButton.SR];

                    output.thumb_stick_left = buttons[(int)ControllerButton.STICK];
                }
            }

            // overwrite guide button if it's custom-mapped
            if (settings.Home != "0")
            {
                output.guide = false;
            }

            if (Type != ControllerType.SNES_CONTROLLER)
            {
                if (other != null || Type == ControllerType.PRO_CONTROLLER)
                { // no need for && other != this
                    output.axis_left_x = CastStickValue((other == input && Type != ControllerType.LEFT_JOYCON) ? stick2[0] : stick[0]);
                    output.axis_left_y = CastStickValue((other == input && Type != ControllerType.LEFT_JOYCON) ? stick2[1] : stick[1]);

                    output.axis_right_x = CastStickValue((other == input && Type != ControllerType.LEFT_JOYCON) ? stick[0] : stick2[0]);
                    output.axis_right_y = CastStickValue((other == input && Type != ControllerType.LEFT_JOYCON) ? stick[1] : stick2[1]);
                }
                else
                { // single joycon mode
                    output.axis_left_y = CastStickValue((Type == ControllerType.LEFT_JOYCON ? 1 : -1) * stick[0]);
                    output.axis_left_x = CastStickValue((Type == ControllerType.LEFT_JOYCON ? -1 : 1) * stick[1]);
                }
            }

            if (other != null || Type == ControllerType.PRO_CONTROLLER)
            {
                byte lval = settings.GyroAnalogSliders ? sliderVal[0] : byte.MaxValue;
                byte rval = settings.GyroAnalogSliders ? sliderVal[1] : byte.MaxValue;
                output.trigger_left = (byte)(buttons[(int)(Type == ControllerType.LEFT_JOYCON ? ControllerButton.SHOULDER_2 : ControllerButton.SHOULDER2_2)] ? lval : 0);
                output.trigger_right = (byte)(buttons[(int)(Type == ControllerType.LEFT_JOYCON ? ControllerButton.SHOULDER2_2 : ControllerButton.SHOULDER_2)] ? rval : 0);
            }
            else
            {
                output.trigger_left = (byte)(buttons[(int)(Type == ControllerType.LEFT_JOYCON ? ControllerButton.SHOULDER_2 : ControllerButton.SHOULDER_1)] ? byte.MaxValue : 0);
                output.trigger_right = (byte)(buttons[(int)(Type == ControllerType.LEFT_JOYCON ? ControllerButton.SHOULDER_1 : ControllerButton.SHOULDER_2)] ? byte.MaxValue : 0);
            }

            return output;
        }
    }
}
