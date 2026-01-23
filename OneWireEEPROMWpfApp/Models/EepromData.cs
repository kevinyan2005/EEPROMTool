using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using OneWire.Common;

namespace OneWireEEPROMWpfApp.Models
{
    public class EepromData
    {
        public OneWireIdentificationBlock Id { get; set; } = new();
        public SensorCalibrationBlock Calibration { get; set; } = new();
        public UserDefinedBlock User { get; set; } = new();
    }
}
