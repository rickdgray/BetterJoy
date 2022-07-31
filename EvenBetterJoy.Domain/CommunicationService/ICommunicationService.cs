using EvenBetterJoy.Domain.Models;

namespace EvenBetterJoy.Domain.Communication
{
    public interface ICommunicationService
    {
        void NewReportIncoming(Joycon hidReport);
        void Start();
        void Stop();
    }
}
