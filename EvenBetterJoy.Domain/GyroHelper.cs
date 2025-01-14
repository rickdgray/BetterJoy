﻿namespace EvenBetterJoy.Domain.Models
{
    public class GyroHelper
    {
        public float SamplePeriod { get; set; }
        public float Beta { get; set; }
        public float[] Quaternion { get; set; }
        public float[] OldPitchYawRoll { get; set; }

        public GyroHelper(float samplePeriod, float beta)
        {
            SamplePeriod = samplePeriod;
            Beta = beta;
            Quaternion = new float[] { 1f, 0f, 0f, 0f };
            OldPitchYawRoll = new float[] { 0f, 0f, 0f };
        }

        public void Update(float gx, float gy, float gz, float ax, float ay, float az)
        {
            // short name local variable for readability
            float q1 = Quaternion[0], q2 = Quaternion[1], q3 = Quaternion[2], q4 = Quaternion[3];
            float norm;
            float s1, s2, s3, s4;
            float qDot1, qDot2, qDot3, qDot4;

            // Auxiliary variables to avoid repeated arithmetic
            float _2q1 = 2f * q1;
            float _2q2 = 2f * q2;
            float _2q3 = 2f * q3;
            float _2q4 = 2f * q4;
            float _4q1 = 4f * q1;
            float _4q2 = 4f * q2;
            float _4q3 = 4f * q3;
            float _8q2 = 8f * q2;
            float _8q3 = 8f * q3;
            float q1q1 = q1 * q1;
            float q2q2 = q2 * q2;
            float q3q3 = q3 * q3;
            float q4q4 = q4 * q4;

            // Normalise accelerometer measurement
            norm = (float)Math.Sqrt(ax * ax + ay * ay + az * az);

            // handle NaN
            if (norm == 0f)
            {
                return;
            }

            // use reciprocal for division
            norm = 1 / norm;
            ax *= norm;
            ay *= norm;
            az *= norm;

            // Gradient decent algorithm corrective step
            s1 = _4q1 * q3q3 + _2q3 * ax + _4q1 * q2q2 - _2q2 * ay;
            s2 = _4q2 * q4q4 - _2q4 * ax + 4f * q1q1 * q2 - _2q1 * ay - _4q2 + _8q2 * q2q2 + _8q2 * q3q3 + _4q2 * az;
            s3 = 4f * q1q1 * q3 + _2q1 * ax + _4q3 * q4q4 - _2q4 * ay - _4q3 + _8q3 * q2q2 + _8q3 * q3q3 + _4q3 * az;
            s4 = 4f * q2q2 * q4 - _2q2 * ax + 4f * q3q3 * q4 - _2q3 * ay;
            
            // normalise step magnitude
            norm = 1f / (float)Math.Sqrt(s1 * s1 + s2 * s2 + s3 * s3 + s4 * s4);
            s1 *= norm;
            s2 *= norm;
            s3 *= norm;
            s4 *= norm;

            // Compute rate of change of quaternion
            qDot1 = 0.5f * (-q2 * gx - q3 * gy - q4 * gz) - Beta * s1;
            qDot2 = 0.5f * (q1 * gx + q3 * gz - q4 * gy) - Beta * s2;
            qDot3 = 0.5f * (q1 * gy - q2 * gz + q4 * gx) - Beta * s3;
            qDot4 = 0.5f * (q1 * gz + q2 * gy - q3 * gx) - Beta * s4;

            // Integrate to yield quaternion
            q1 += qDot1 * SamplePeriod;
            q2 += qDot2 * SamplePeriod;
            q3 += qDot3 * SamplePeriod;
            q4 += qDot4 * SamplePeriod;
            
            // normalise quaternion
            norm = 1f / (float)Math.Sqrt(q1 * q1 + q2 * q2 + q3 * q3 + q4 * q4);
            Quaternion[0] = q1 * norm;
            Quaternion[1] = q2 * norm;
            Quaternion[2] = q3 * norm;
            Quaternion[3] = q4 * norm;
        }

        public float[] GetEulerAngles()
        {
            float[] pitchYawRoll = new float[3];
            float q0 = Quaternion[0], q1 = Quaternion[1], q2 = Quaternion[2], q3 = Quaternion[3];
            float sq1 = q1 * q1, sq2 = q2 * q2, sq3 = q3 * q3;
            pitchYawRoll[0] = (float)Math.Asin(2f * (q0 * q2 - q3 * q1));                            // Pitch 
            pitchYawRoll[1] = (float)Math.Atan2(2f * (q0 * q3 + q1 * q2), 1 - 2f * (sq2 + sq3));     // Yaw
            pitchYawRoll[2] = (float)Math.Atan2(2f * (q0 * q1 + q2 * q3), 1 - 2f * (sq1 + sq2));     // Roll 

            float[] returnAngles = new float[6];
            Array.Copy(pitchYawRoll, returnAngles, 3);
            Array.Copy(OldPitchYawRoll, 0, returnAngles, 3, 3);
            OldPitchYawRoll = pitchYawRoll;

            return returnAngles;
        }
    }
}
