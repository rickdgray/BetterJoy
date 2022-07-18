namespace EvenBetterJoy.Services
{
    public interface ICommunicationService
    {
        void NewReportIncoming(Joycon hidReport);
        void Start();
        void Stop();
    }
}