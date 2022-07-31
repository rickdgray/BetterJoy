using EvenBetterJoy.Domain.Models;

namespace EvenBetterJoy.Domain.Services
{
    public interface ICommunicationService
    {
        void NewReportIncoming(Joycon hidReport);
        void Start();
        void Stop();
    }
}
