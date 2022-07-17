using System.Net;

namespace EvenBetterJoy.Services
{
    public interface ICommunicationService
    {
        void NewReportIncoming(Joycon hidReport);
        void Start(IPAddress ip, int port = 26760);
        void Stop();
    }
}