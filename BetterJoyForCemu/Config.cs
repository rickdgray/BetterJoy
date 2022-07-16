using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using BetterJoyForCemu.Models;

namespace BetterJoyForCemu
{
    public static class Config
    {
        // stores dynamic configuration, including
        static readonly string path;
        static readonly Dictionary<string, string> Settings = new();

        // currently - ProgressiveScan, StartInTray + special buttons
        const int SettingsNum = 11;

        static Config()
        {
            path = $"{Path.GetDirectoryName(AppContext.BaseDirectory)}\\settings";
        }

        public static string GetDefaultValue(string s)
        {
            return s switch
            {
                "ProgressiveScan" => "1",
                "capture" => $"key_{WindowsInput.Events.KeyCode.PrintScreen}",
                "reset_mouse" => $"joy_{Joycon.Button.STICK}",
                _ => "0",
            };
        }

        //TODO: probably could just replace this whole thing with a json serializer
        public static void Init(List<KeyValuePair<string, float[]>> caliData)
        {
            //TODO: this long string needs to be separated
            foreach (var s in new string[] { "ProgressiveScan", "StartInTray", "capture", "home", "sl_l", "sl_r", "sr_l", "sr_r", "shake", "reset_mouse", "active_gyro" })
            {
                Settings[s] = GetDefaultValue(s);
            }

            if (File.Exists(path))
            {
                //TODO: maybe switch to IOptions<Settings>?
                var settings = JsonSerializer.Deserialize<Settings>(File.ReadAllText(path));

                using var file = new StreamReader(path);
                var line = string.Empty;
                int lineNO = 0;
                while ((line = file.ReadLine()) != null)
                {
                    string[] vs = line.Split();

                    if (lineNO < SettingsNum)
                    {
                        // load in basic settings
                        Settings[vs[0]] = vs[1];
                    }
                    else
                    {
                        // load in calibration presets
                        caliData.Clear();
                        for (int i = 0; i < vs.Length; i++)
                        {
                            string[] caliArr = vs[i].Split(',');
                            float[] newArr = new float[6];
                            for (int j = 1; j < caliArr.Length; j++)
                            {
                                newArr[j - 1] = float.Parse(caliArr[j]);
                            }
                            caliData.Add(new KeyValuePair<string, float[]>(
                                caliArr[0],
                                newArr
                            ));
                        }
                    }

                    lineNO++;
                }
            }
            else
            {
                using var file = new StreamWriter(path);
                foreach (string k in Settings.Keys)
                {
                    file.WriteLine(string.Format("{0} {1}", k, Settings[k]));
                }

                string caliStr = "";
                for (int i = 0; i < caliData.Count; i++)
                {
                    string space = " ";
                    if (i == 0) space = "";
                    caliStr += $"{space}{caliData[i].Key},{string.Join(",", caliData[i].Value)}";
                }
                file.WriteLine(caliStr);
            }
        }

        public static int IntValue(string key)
        {
            if (!Settings.ContainsKey(key))
            {
                return 0;
            }

            return int.Parse(Settings[key]);
        }

        public static string Value(string key)
        {
            if (!Settings.ContainsKey(key))
            {
                return "";
            }

            return Settings[key];
        }

        public static bool SetValue(string key, string value)
        {
            if (!Settings.ContainsKey(key))
                return false;
            Settings[key] = value;
            return true;
        }

        public static void SaveCaliData(List<KeyValuePair<string, float[]>> caliData)
        {
            string[] txt = File.ReadAllLines(path);

            // no custom calibrations yet
            if (txt.Length < SettingsNum + 1)
            {
                Array.Resize(ref txt, txt.Length + 1);
            }

            var caliStr = string.Empty;
            for (int i = 0; i < caliData.Count; i++)
            {
                string space = " ";
                if (i == 0) space = string.Empty;
                caliStr += $"{space}{caliData[i].Key},{string.Join(",", caliData[i].Value)}";
            }
            txt[SettingsNum] = caliStr;
            File.WriteAllLines(path, txt);
        }

        public static void Save()
        {
            string[] txt = File.ReadAllLines(path);
            var NO = 0;
            foreach (var k in Settings.Keys)
            {
                txt[NO] = string.Format("{0} {1}", k, Settings[k]);
                NO++;
            }
            File.WriteAllLines(path, txt);
        }
    }
}
