namespace EvenBetterJoy.Domain.Services
{
    public interface IHidGuardianService
    {
        void Start();
        void Stop();
        void Block(string path);
    }
}
