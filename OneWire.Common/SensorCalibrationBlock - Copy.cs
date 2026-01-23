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

        //public const int TotalSize = 40;

        //public const int OffsetCrc = 38;

        //public byte[] ToBytes()
        //{
        //    byte[] buffer = new byte[TotalSize];

        //    // 1. GaugeFactors (4 x 4 bytes)
        //    for (int i = 0; i < 4; i++)
        //    {
        //        WriteUint32Le(buffer, GaugeFactors[i], i * 4);
        //    }

        //    // 2. ReferenceValue (Offset 16)
        //    WriteUint32Le(buffer, ReferenceValue, 16);

        //    // 3. Dates (Offset 20 and 28)
        //    // DateTime.ToBinary() returns a 64-bit (8 byte) value
        //    WriteInt64Le(buffer, ManufactureDate.ToBinary(), 20);
        //    WriteInt64Le(buffer, ExpiryDate.ToBinary(), 28);

        //    // 4. GaugeType (Offset 36 - 2 bytes ASCII)
        //    WriteFixedString(buffer, GaugeType, 36, 2);

        //    // 5. CRC16 (Offset 38)
        //    this.Crc16 = CrcHelper.ComputeCrc16(buffer, LengthWithoutCrc);

        //    // 3. Write the CRC to the last 2 bytes
        //    WriteUint16Le(buffer, this.Crc16, OffsetCrc);
        //    return buffer;
        //}

        //private void WriteUint32Le(byte[] buffer, uint value, int offset)
        //{
        //    byte[] data = BitConverter.GetBytes(value);
        //    if (!BitConverter.IsLittleEndian) Array.Reverse(data);
        //    Buffer.BlockCopy(data, 0, buffer, offset, 4);
        //}

        //private void WriteInt64Le(byte[] buffer, long value, int offset)
        //{
        //    byte[] data = BitConverter.GetBytes(value);
        //    if (!BitConverter.IsLittleEndian) Array.Reverse(data);
        //    Buffer.BlockCopy(data, 0, buffer, offset, 8);
        //}

        //private void WriteUint16Le(byte[] buffer, ushort value, int offset)
        //{
        //    byte[] data = BitConverter.GetBytes(value);
        //    if (!BitConverter.IsLittleEndian) Array.Reverse(data);
        //    Buffer.BlockCopy(data, 0, buffer, offset, 2);
        //}

        //private void WriteFixedString(byte[] buffer, string text, int offset, int length)
        //{
        //    if (string.IsNullOrEmpty(text)) text = string.Empty;
        //    byte[] stringBytes = Encoding.ASCII.GetBytes(text);
        //    int bytesToCopy = Math.Min(stringBytes.Length, length);
        //    Array.Copy(stringBytes, 0, buffer, offset, bytesToCopy);
        //}

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
