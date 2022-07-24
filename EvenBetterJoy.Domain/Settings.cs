using System.Net;

namespace EvenBetterJoy.Domain.Models
{
    public class Settings
    {
        public bool ProgressiveScan { get; set; } = true;
        public bool UseHidg { get; set; }
        public bool MotionServer { get; set; }
        public bool StartInTray { get; set; }
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
        public float AhrsBeta { get; set; }
        public bool ShakeInputEnabled { get; set; }
        public bool DragToggle { get; set; }
        public bool HomeLongPowerOff { get; set; }
        public long PowerOffInactivity { get; set; }
        public bool ChangeOrientationDoubleClick { get; set; }
        public string GyroToJoyOrMouse { get; set; }
        public bool UseFilteredIMU { get; set; }
        public int GyroMouseSensitivityX { get; set; }
        public int GyroMouseSensitivityY { get; set; }
        public float GyroStickSensitivityX { get; set; }
        public float GyroStickSensitivityY { get; set; }
        public float GyroStickReduction { get; set; }
        public bool GyroHoldToggle { get; set; }
        public bool GyroAnalogSliders { get; set; }
        public int GyroAnalogSensitivity { get; set; }
        public bool GyroMouseLeftHanded { get; set; }
        public bool SwapAB { get; set; }
        public bool SwapXY { get; set; }
        public string acc_sensiti { get; set; }
        public string gyr_sensiti { get; set; }
        public string stick_cal { get; set; }
        public ushort deadzone { get; set; }
        public string stick2_cal { get; set; }
        public ushort deadzone2 { get; set; }
        //Controller mapping
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
        public string ActiveGyro { get; set; }
    }
}
