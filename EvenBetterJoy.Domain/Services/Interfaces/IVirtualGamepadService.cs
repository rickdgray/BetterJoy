using Nefarius.ViGEm.Client;

namespace EvenBetterJoy.Domain.Services
{
    public interface IVirtualGamepadService
    {
        void Start();
        ViGEmClient Get();
    }
}
