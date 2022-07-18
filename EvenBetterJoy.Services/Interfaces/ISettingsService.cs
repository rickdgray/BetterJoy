using EvenBetterJoy.Models;

namespace EvenBetterJoy.Services
{
    public interface ISettingsService
    {
        Settings Settings { get; set; }

        void Load();
        void Save();
    }
}