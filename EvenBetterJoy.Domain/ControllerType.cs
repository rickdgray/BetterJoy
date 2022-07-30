namespace EvenBetterJoy.Domain.Models
{
    public enum ControllerType : ushort
    {
        UNKNOWN = 0x0000,
        LEFT_JOYCON = 0x2006,
        RIGHT_JOYCON = 0x2007,
        PRO_CONTROLLER = 0x2009,
        //NES_CONTROLLER = 0x????,
        SNES_CONTROLLER = 0x2017,
        N64_CONTROLLER = 0x2019
    }
}
