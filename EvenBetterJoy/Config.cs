using System;
using System.IO;
using System.Text.Json;
using EvenBetterJoy.Models;

namespace EvenBetterJoy
{
    public class Config
    {
        public readonly string path;
        public Settings Settings { get; set; }

        public Config()
        {
            path = $"{Path.GetDirectoryName(AppContext.BaseDirectory)}\\settings";
        }
        
        public void Load()
        {
            if (File.Exists(path))
            {
                Settings = JsonSerializer.Deserialize<Settings>(File.ReadAllText(path));
            }
            else
            {
                Settings = new Settings();
            }
        }

        public void Save()
        {
            File.WriteAllText(path, JsonSerializer.Serialize(Settings));
        }
    }
}
