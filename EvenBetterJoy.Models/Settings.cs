namespace EvenBetterJoy.Models
{
    public class Settings
    {
        public bool ProgressiveScan { get; set; } = true;
        public bool StartInTray { get; set; }
        //TODO: all these buttons should be enums
        public string Capture { get; set; } = $"key_{WindowsInput.Events.KeyCode.PrintScreen}";
        public string Home { get; set; }
        //TODO: change these from l_l to something better
        public string Sl_l { get; set; }
        public string Sl_r { get; set; }
        public string Sr_l { get; set; }
        public string Sr_r { get; set; }
        public string Shake { get; set; }
        public string ResetMouse { get; set; } = $"joy_{ControllerButton.STICK}";
        public int ActiveGyro { get; set; }
        //TODO: probably can switch off array here
        public List<KeyValuePair<string, float[]>> CalibrationData { get; set; }
    }
}
