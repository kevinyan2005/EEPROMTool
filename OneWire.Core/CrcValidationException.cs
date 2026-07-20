using System;

namespace OneWire.Core
{
    public class CrcValidationException : Exception
    {
        public EepromData EepromData { get; }

        public CrcValidationException(string message, EepromData eepromData) : base(message)
        {
            EepromData = eepromData;
        }
    }
}
