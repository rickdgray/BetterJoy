namespace EvenBetterJoy.Terminal
{
    public interface IJoyconManager
    {
        void CheckForNewControllers();
        void Stop();
        void Start();
    }
}