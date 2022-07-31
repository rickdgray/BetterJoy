namespace EvenBetterJoy.Domain.HidHide
{
    public interface IHidHideService
    {
        void Block(string path);
        void Block(IEnumerable<string> paths);
        void Unblock(string path);
        void Unblock(IEnumerable<string> paths);
    }
}
