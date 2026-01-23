using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OneWire.Common
{
    public class OneWireIdentificationBlock : IBlockWithCrc
    {
        public const int LengthWithoutCrc = 36;
        public ushort DataVersion { get; set; }      // 2 bytes
        public ushort DataId { get; set; }           // 2 bytes EEPROM chip ID
        public string Model { get; set; }            // 16 bytes (ASCII), e.g. DS2431
        public string SerialNumber { get; set; }     // 16 bytes (ASCII)
        public ushort Crc16 { get; set; }            // 2 bytes (calculated later)

        public byte[] ToBytes()
        {
            byte[] data = new byte[36];
            int offset = 0;

            Array.Copy(BitConverter.GetBytes(DataVersion), 0, data, offset, 2);
            offset += 2;

            Array.Copy(BitConverter.GetBytes(DataId), 0, data, offset, 2);
            offset += 2;

            byte[] modelBytes = Encoding.ASCII.GetBytes(Model ?? "");
            Array.Copy(modelBytes, 0, data, offset, Math.Min(16, modelBytes.Length));
            offset += 16;

            byte[] serialBytes = Encoding.ASCII.GetBytes(SerialNumber ?? "");
            Array.Copy(serialBytes, 0, data, offset, Math.Min(16, serialBytes.Length));

            // Compute CRC16 using shared helper
            Crc16 = CrcHelper.ComputeCrc16(data, data.Length);

            // Append CRC16 to final buffer
            byte[] withCrc = new byte[data.Length + 2];
            Array.Copy(data, withCrc, data.Length);
            Array.Copy(BitConverter.GetBytes(Crc16), 0, withCrc, data.Length, 2);

            return withCrc;
        }
        
        public bool ValidateCrc()
        {
            return Crc16 == CrcHelper.ComputeCrc16(ToBytes(), LengthWithoutCrc );
        }

        public void RecalculateCrc()
        {
            Crc16 = CrcHelper.ComputeCrc16(ToBytes(), LengthWithoutCrc);
        }
    }
}
