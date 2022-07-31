using Nefarius.ViGEm.Client;

namespace EvenBetterJoy.Domain.VirtualController
{
    public interface IVirtualControllerService
    {
        void Start();
        ViGEmClient Get();
    }
}
