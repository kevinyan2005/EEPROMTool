using System;
using System.Text;

namespace OneWire.Common
{
    public class OneWireIdentificationBlock : IBlockWithCrc
    {
        public const int LengthWithoutCrc = EepromLayout.IdBlockLength;

        public ushort DataVersion { get; set; }      // 2 bytes
        public string DataId { get; set; }           // 2 bytes EEPROM chip ID
        public string Model { get; set; }            // 16 bytes (ASCII), e.g. DS2431
        public string SerialNumber { get; set; }     // 16 bytes (ASCII)
        public ushort Crc16 { get; set; }            // 2 bytes (calculated later)

        public byte[] ToBytes()
        {
            byte[] data = new byte[EepromLayout.IdBlockLength];
            int offset = 0;

            byte[] versionBytes = EndianHelper.ConvertUInt16ToBytesBigEndian(DataVersion);
            Array.Copy(versionBytes, 0, data, offset, Math.Min(EepromLayout.IdVersionSize, versionBytes.Length));
            offset += EepromLayout.IdVersionSize;

            byte[] idBytes = Encoding.ASCII.GetBytes(DataId ?? "");
            Array.Copy(idBytes, 0, data, offset, Math.Min(EepromLayout.IdDataIdSize, idBytes.Length));
            offset += EepromLayout.IdDataIdSize;

            byte[] modelBytes = Encoding.ASCII.GetBytes(Model ?? "");
            Array.Copy(modelBytes, 0, data, offset, Math.Min(EepromLayout.IdModelSize, modelBytes.Length));
            offset += EepromLayout.IdModelSize;

            byte[] serialBytes = Encoding.ASCII.GetBytes(SerialNumber ?? "");
            Array.Copy(serialBytes, 0, data, offset, Math.Min(EepromLayout.IdSerialSize, serialBytes.Length));

            Crc16 = CrcHelper.ComputeCrc16(data, data.Length);

            byte[] withCrc = new byte[EepromLayout.IdBlockLength + EepromLayout.CrcSize];
            Array.Copy(data, withCrc, data.Length);

            var crc16Bytes = BitConverter.GetBytes(Crc16);
            if (BitConverter.IsLittleEndian)
                Array.Reverse(crc16Bytes);
            Array.Copy(crc16Bytes, 0, withCrc, data.Length, EepromLayout.CrcSize);

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
