using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OneWire.Common
{
    public class UserDefinedBlock : IBlockWithCrc
    {
        public const int LengthWithoutCrc = 40;
        public uint ZeroValue { get; set; }                 // 4 bytes
        public uint EqualizationFactor { get; set; }        // 4 bytes
        public string ProbeSerialNumber { get; set; } = ""; // 16 bytes (ASCII)
        public DateTime ProbeManufactureDate { get; set; } = DateTime.Now.AddYears(2);  // 8 bytes
        public DateTime FirstConnectionDate { get; set; } = DateTime.MinValue; // 8 bytes
        public ushort Crc16 { get; set; }           // 2 bytes
       
        public byte[] ToBytes()
        {
            // Excluding CRC: 40 bytes (2+4+4+16+8+8)
            byte[] data = new byte[40];
            int offset = 0;
            
            // ZeroValue
            Array.Copy(BitConverter.GetBytes(ZeroValue), 0, data, offset, 4);
            offset += 4;

            // EqualizationFactor
            Array.Copy(BitConverter.GetBytes(EqualizationFactor), 0, data, offset, 4);
            offset += 4;

            // ProbeSerialNumber (ASCII padded to 16 bytes)
            byte[] probeSerialBytes = Encoding.ASCII.GetBytes(ProbeSerialNumber ?? "");
            Array.Copy(probeSerialBytes, 0, data, offset, Math.Min(16, probeSerialBytes.Length));
            offset += 16;

            // ProbeManufactureDate
            Array.Copy(BitConverter.GetBytes(ProbeManufactureDate.ToBinary()), 0, data, offset, 8);
            offset += 8;

            // FirstConnectionDate
            Array.Copy(BitConverter.GetBytes(FirstConnectionDate.ToBinary()), 0, data, offset, 8);

            // CRC16
            Crc16 = CrcHelper.ComputeCrc16(data, data.Length);
            byte[] withCrc = new byte[data.Length + 2];
            Array.Copy(data, withCrc, data.Length);
            Array.Copy(BitConverter.GetBytes(Crc16), 0, withCrc, data.Length, 2);

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
