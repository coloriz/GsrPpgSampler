namespace InsLab.Signal
{
    public class GsrPpgPacket
    {
        public string TypeName { get; } = nameof(GsrPpgPacket);
        public long Timestamp { get; set; }
        public int Value { get; set; }

        public GsrPpgPacket(int value, long timestamp)
        {
            Value = value;
            Timestamp = timestamp;
        }

        public override string ToString()
        {
            return $"Value = {Value}, timestamp = {Timestamp}";
        }
    }
}
