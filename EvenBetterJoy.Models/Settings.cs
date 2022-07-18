using System.Net;

namespace EvenBetterJoy.Models
{
    public class Settings
    {
        public bool ProgressiveScan { get; set; } = true;
        public bool UseHidg { get; set; }
        public bool MotionServer { get; set; }
        public bool StartInTray { get; set; }
        //TODO: all these buttons should be enums
        public string Capture { get; set; } = $"key_{WindowsInput.Events.KeyCode.PrintScreen}";
        public string Home { get; set; }
        //TODO: change these from l_l to something better
        public string LeftJoyconL { get; set; }
        public string LeftJoyconR { get; set; }
        public string RightJoyconL { get; set; }
        public string RightJoyconR { get; set; }
        public string Shake { get; set; }
        public string ResetMouse { get; set; } = $"joy_{ControllerButton.STICK}";
        public int ActiveGyro { get; set; }
        //TODO: probably can switch off array here
        public List<KeyValuePair<string, float[]>> CalibrationData { get; set; }
        public ControllerDebugMode ControllerDebugMode { get; set; }
        public int LowFreqRumble { get; set; }
        public int HighFreqRumble { get; set; }
        public bool EnableRumble { get; set; }
        public bool ShowAsXInput { get; set; }
        public bool ShowAsDS4 { get; set; }
        public bool UseIncrementalLights { get; set; }
        public bool PurgeWhitelist { get; set; }
        public bool PurgeAffectedDevices { get; set; }
        public IPAddress IpAddress { get; set; }
        public int Port { get; set; }
        public bool HomeLedOn { get; set; }
        public bool AutoPowerOff { get; set; }
    }
}
