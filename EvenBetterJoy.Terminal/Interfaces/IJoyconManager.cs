namespace EvenBetterJoy.Terminal
{
    public interface IJoyconManager
    {
        void Start(CancellationToken cancellationToken);
        void Stop(CancellationToken cancellationToken);
    }
}
