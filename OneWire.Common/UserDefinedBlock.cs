using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OneWire.Common
{
    public class UserDefinedBlock : IBlockWithCrc
    {
        public const int LengthWithoutCrc = 26;
        public ushort Schema { get; set; }                 // 2 bytes
        public string ProbeSerialNumber { get; set; } = ""; // 16 bytes (ASCII)
        public DateTime ProbeExpiryDate { get; set; } = DateTime.Now.AddYears(2);  // 8 bytes
        public ushort Crc16 { get; set; }           // 2 bytes
        public DateTime ProbeUsageDate { get; set; } = DateTime.MaxValue; // 8 bytes

        public byte[] ToBytes()
        {
            // Excluding CRC: 26 bytes (2+16+8)
            byte[] data = new byte[LengthWithoutCrc];
            int offset = 0;

            // Schema
            byte[] schemaBytes = BitConverter.GetBytes(Schema);
            Array.Copy(schemaBytes, 0, data, offset, Math.Min(2, schemaBytes.Length));
            offset += 2;

            // ProbeSerialNumber (ASCII padded to 16 bytes)
            byte[] probeSerialBytes = Encoding.ASCII.GetBytes(ProbeSerialNumber ?? "");
            Array.Copy(probeSerialBytes, 0, data, offset, Math.Min(16, probeSerialBytes.Length));
            offset += 16;

            // ProbeExpiryDate
            Array.Copy(BitConverter.GetBytes(ProbeExpiryDate.ToBinary()), 0, data, offset, 8);
            offset += 8;

            // CRC16
            Crc16 = CrcHelper.ComputeCrc16(data, data.Length);
            byte[] withCrc = new byte[data.Length + 10];
            Array.Copy(data, withCrc, data.Length);
            Array.Copy(BitConverter.GetBytes(Crc16), 0, withCrc, data.Length, 2);

            // ProbeUsageDate
            byte[] pudBytes = BitConverter.GetBytes(ProbeUsageDate.ToBinary());
            Array.Copy(pudBytes, 0, withCrc, 28, Math.Min(8, pudBytes.Length));

            return withCrc;
        }

        public bool ValidateCrc()
        {
            return Crc16 == CrcHelper.ComputeCrc16(ToBytes(), LengthWithoutCrc);
        }

        public void RecalculateCrc()
        {
            Crc16 = CrcHelper.ComputeCrc16(ToBytes(), LengthWithoutCrc);
        }
    }
}
