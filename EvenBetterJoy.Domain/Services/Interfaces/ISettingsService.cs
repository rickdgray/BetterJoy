using EvenBetterJoy.Domain.Models;

namespace EvenBetterJoy.Domain.Services
{
    public interface ISettingsService
    {
        Settings Settings { get; set; }

        void Load();
        void Save();
    }
}
