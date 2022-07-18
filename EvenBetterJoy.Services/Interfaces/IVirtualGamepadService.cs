using Nefarius.ViGEm.Client;

namespace EvenBetterJoy.Services
{
    public interface IVirtualGamepadService
    {
        void Start();
        ViGEmClient Get();
    }
}