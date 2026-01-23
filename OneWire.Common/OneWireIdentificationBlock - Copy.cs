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

        // Constants for offsets
        //private const int OffsetVersion = 0;
        //private const int OffsetId = 2;
        //private const int OffsetModel = 4;
        //private const int OffsetSerial = 20;
        //private const int OffsetCrc = 36;
        //private const int TotalSize = 38;
        //private const int LenSerial = 16;
        //private const int LenModel = 16;

        //public byte[] ToBytes()
        //{
        //    byte[] buffer = new byte[TotalSize]; // 40 bytes

        //    // 1. Serialize all data into buffer (indices 0 to 37)
        //    WriteUint16Le(buffer, DataVersion, OffsetVersion );
        //    WriteUint16Le(buffer, DataId, OffsetId);
        //    WriteFixedString(buffer, Model, OffsetModel,LenModel);
        //    WriteFixedString(buffer, SerialNumber, OffsetSerial, LenSerial);

        //    // 2. Calculate CRC on the first 38 bytes
        //    this.Crc16 = CrcHelper.ComputeCrc16(buffer, LengthWithoutCrc);

        //    // 3. Write the CRC to the last 2 bytes
        //    WriteUint16Le(buffer, this.Crc16, OffsetCrc);

        //    return buffer;
        //}


        //private void WriteUint16Le(byte[] buffer, ushort value, int offset)
        //{
        //    byte[] data = BitConverter.GetBytes(value);
        //    // If the system is NOT Little Endian, reverse it to force LE
        //    if (!BitConverter.IsLittleEndian)
        //    {
        //        Array.Reverse(data);
        //    }
        //    Buffer.BlockCopy(data, 0, buffer, offset, 2);
        //}

        //private void WriteFixedString(byte[] buffer, string text, int offset, int length)
        //{
        //    if (text == null) text = string.Empty;
        //    byte[] stringBytes = Encoding.ASCII.GetBytes(text);
        //    int bytesToCopy = Math.Min(stringBytes.Length, length);
        //    Array.Copy(stringBytes, 0, buffer, offset, bytesToCopy);
        //}

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
