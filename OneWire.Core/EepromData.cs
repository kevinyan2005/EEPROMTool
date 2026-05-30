namespace OneWire.Core
{
    public class EepromData
    {
        public OneWireIdentificationBlock Id { get; set; } = new OneWireIdentificationBlock();
        public SensorCalibrationBlock Calibration { get; set; } = new SensorCalibrationBlock();
        public UserDefinedBlock User { get; set; } = new UserDefinedBlock();
    }
}
