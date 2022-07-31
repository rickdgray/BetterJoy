namespace EvenBetterJoy.Terminal
{
    internal interface IEvenBetterJoyApplication
    {
        void Start(CancellationToken cancellationToken);
        void Stop(CancellationToken cancellationToken);
    }
}
