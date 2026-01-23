using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OneWire.Common
{
    public class SensorCalibrationBlock : IBlockWithCrc
    {
        public const int LengthWithoutCrc = 38;
        public uint[] GaugeFactors { get; set; } = new uint[4];  // 4 × 4 bytes = 16
        public uint ReferenceValue { get; set; }                 // 4 bytes
        public DateTime ManufactureDate { get; set; }            // 8 bytes
        public DateTime ExpiryDate { get; set; }                 // 8 bytes
        //public ushort GaugeType { get; set; }                    // 2 bytes
        public string GaugeType { get; set; }                    // 2 bytes

        public ushort Crc16 { get; set; }                        // 2 bytes
        
        /// <summary>
        /// Convert block to byte buffer including CRC16.
        /// </summary>
        public byte[] ToBytes()
        {
            byte[] data = new byte[38];
            int offset = 0;

            // GaugeFactors
            for (int i = 0; i < 4; i++)
            {
                Array.Copy(BitConverter.GetBytes(GaugeFactors[i]), 0, data, offset, 4);
                offset += 4;
            }

            // ReferenceValue
            Array.Copy(BitConverter.GetBytes(ReferenceValue), 0, data, offset, 4);
            offset += 4;

            // ManufactureDate (use DateTime.ToBinary(), 8 bytes)
            Array.Copy(BitConverter.GetBytes(ManufactureDate.ToBinary()), 0, data, offset, 8);
            offset += 8;

            // ExpiryDate (use DateTime.ToBinary(), 8 bytes)
            Array.Copy(BitConverter.GetBytes(ExpiryDate.ToBinary()), 0, data, offset, 8);
            offset += 8;

            // GaugeType
            //Array.Copy(BitConverter.GetBytes(GaugeType), 0, data, offset, 2);
            byte[] gaugeBytes = Encoding.ASCII.GetBytes(GaugeType ?? "");
            Array.Copy(gaugeBytes, 0, data, offset, Math.Min(2, gaugeBytes.Length));
            offset += 2;


            // Compute CRC16
            Crc16 = CrcHelper.ComputeCrc16(data, data.Length);

            // Final buffer with CRC16 appended
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
