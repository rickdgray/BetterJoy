namespace EvenBetterJoy.Models
{
    public class Rumble
    {
        public Queue<float[]> queue;

        public Rumble(float[] rumble_info)
        {
            queue = new Queue<float[]>();
            queue.Enqueue(rumble_info);
        }

        public void SetVals(float low_freq, float high_freq, float amplitude)
        {
            float[] rumbleQueue = new float[] { low_freq, high_freq, amplitude };
            // Keep a queue of 15 items, discard oldest item if queue is full.
            if (queue.Count > 15)
            {
                queue.Dequeue();
            }
            queue.Enqueue(rumbleQueue);
        }
        
        private static float Clamp(float x, float min, float max)
        {
            if (x < min) return min;
            if (x > max) return max;
            return x;
        }

        private static byte EncodeAmp(float amp)
        {
            byte en_amp;

            if (amp == 0)
                en_amp = 0;
            else if (amp < 0.117)
                en_amp = (byte)(((Math.Log(amp * 1000, 2) * 32) - 0x60) / (5 - Math.Pow(amp, 2)) - 1);
            else if (amp < 0.23)
                en_amp = (byte)(((Math.Log(amp * 1000, 2) * 32) - 0x60) - 0x5c);
            else
                en_amp = (byte)((((Math.Log(amp * 1000, 2) * 32) - 0x60) * 2) - 0xf6);

            return en_amp;
        }

        public byte[] GetData()
        {
            byte[] rumble_data = new byte[8];
            float[] queued_data = queue.Dequeue();

            if (queued_data[2] == 0.0f)
            {
                rumble_data[0] = 0x0;
                rumble_data[1] = 0x1;
                rumble_data[2] = 0x40;
                rumble_data[3] = 0x40;
            }
            else
            {
                queued_data[0] = Clamp(queued_data[0], 40.875885f, 626.286133f);
                queued_data[1] = Clamp(queued_data[1], 81.75177f, 1252.572266f);

                queued_data[2] = Clamp(queued_data[2], 0.0f, 1.0f);

                ushort hf = (ushort)((Math.Round(32f * Math.Log(queued_data[1] * 0.1f, 2)) - 0x60) * 4);
                byte lf = (byte)(Math.Round(32f * Math.Log(queued_data[0] * 0.1f, 2)) - 0x40);
                byte hf_amp = EncodeAmp(queued_data[2]);

                ushort lf_amp = (ushort)(Math.Round((double)hf_amp) * .5);
                byte parity = (byte)(lf_amp % 2);
                if (parity > 0)
                {
                    --lf_amp;
                }

                lf_amp = (ushort)(lf_amp >> 1);
                lf_amp += 0x40;
                if (parity > 0) lf_amp |= 0x8000;

                hf_amp = (byte)(hf_amp - (hf_amp % 2)); // make even at all times to prevent weird hum
                rumble_data[0] = (byte)(hf & 0xff);
                rumble_data[1] = (byte)(((hf >> 8) & 0xff) + hf_amp);
                rumble_data[2] = (byte)(((lf_amp >> 8) & 0xff) + lf);
                rumble_data[3] = (byte)(lf_amp & 0xff);
            }

            for (int i = 0; i < 4; ++i)
            {
                rumble_data[4 + i] = rumble_data[i];
            }

            return rumble_data;
        }
    }
}
