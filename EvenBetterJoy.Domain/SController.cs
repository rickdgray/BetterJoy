namespace EvenBetterJoy.Domain
{
    public class SController
    {
        public string name;
        public ushort product_id;
        public ushort vendor_id;
        public string serial_number;
        public byte type; // 1 is pro, 2 is left joy, 3 is right joy

        public SController(string name, ushort vendor_id, ushort product_id, byte type, string serial_number)
        {
            this.product_id = product_id; this.vendor_id = vendor_id; this.type = type;
            this.serial_number = serial_number;
            this.name = name;
        }

        public override bool Equals(object obj)
        {
            //Check for null and compare run-time types.
            if ((obj == null) || !GetType().Equals(obj.GetType()))
            {
                return false;
            }
            else
            {
                SController s = (SController)obj;
                return (s.product_id == product_id) && (s.vendor_id == vendor_id) && (s.serial_number == serial_number);
            }
        }

        public override int GetHashCode()
        {
            return Tuple.Create(product_id, vendor_id, serial_number).GetHashCode();
        }

        public override string ToString()
        {
            return name ?? $"Unidentified Device ({product_id})";
        }

        public string Serialise()
        {
            return string.Format("{0}|{1}|{2}|{3}|{4}", name, vendor_id, product_id, type, serial_number);
        }
    }
}
