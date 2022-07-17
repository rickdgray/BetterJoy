namespace EvenBetterJoy.Services
{
    public interface IGyroService
    {
        float Beta { get; set; }
        float[] old_pitchYawRoll { get; set; }
        float[] Quaternion { get; set; }
        float SamplePeriod { get; set; }

        float[] GetEulerAngles();
        void Update(float gx, float gy, float gz, float ax, float ay, float az);
    }
}