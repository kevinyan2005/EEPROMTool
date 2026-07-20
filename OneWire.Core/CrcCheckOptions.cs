namespace OneWire.Core
{
    public class CrcCheckOptions
    {
        public bool CheckIdentification { get; set; } = true;
        public bool CheckCalibration { get; set; } = true;
        public bool CheckUser { get; set; } = true;
    }
}
