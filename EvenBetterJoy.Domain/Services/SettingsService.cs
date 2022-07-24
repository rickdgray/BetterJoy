using System.Text.Json;
using EvenBetterJoy.Domain.Models;

namespace EvenBetterJoy.Domain.Services
{
    public class SettingsService : ISettingsService
    {
        public readonly string path;
        public Settings Settings { get; set; }

        public SettingsService()
        {
            path = $"{Path.GetDirectoryName(AppContext.BaseDirectory)}\\settings";
            Load();
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
