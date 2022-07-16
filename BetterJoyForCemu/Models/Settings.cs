using System.Collections.Generic;

namespace BetterJoyForCemu.Models
{
    public class Settings
    {
        public bool ProgressiveScan { get; set; }
        public bool StartInTray { get; set; }
        public int Capture { get; set; }
        public int Home { get; set; }
        //TODO: change these from l_l to something better
        public int Sl_l { get; set; }
        public int Sl_r { get; set; }
        public int Sr_l { get; set; }
        public int Sr_r { get; set; }
        public int Shake { get; set; }
        public int ResetMouse { get; set; }
        public int ActiveGyro { get; set; }
        //TODO: probably can switch off array here
        public Dictionary<string, float[]> CalibrationData { get; set; }
    }
}
