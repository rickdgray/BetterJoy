namespace EvenBetterJoy.Domain
{
    public interface IJoyconManager
    {
        Task Start(CancellationToken cancellationToken);
        void Stop(CancellationToken cancellationToken);
    }
}
