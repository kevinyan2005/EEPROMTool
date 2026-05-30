using OneWire.Common;

namespace OneWire.Services
{
    public interface IEepromSerializer
    {
        /// <summary>Decode a 128-byte EEPROM image into the domain model.</summary>
        EepromData Decode(byte[] raw);

        /// <summary>Encode the domain model into a full 128-byte EEPROM image (0xFF-padded).</summary>
        byte[] Encode(EepromData data);
    }
}
