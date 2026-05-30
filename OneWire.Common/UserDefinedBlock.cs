using System;
using System.Text;

namespace OneWire.Common
{
    public class UserDefinedBlock : IBlockWithCrc
    {
        public const int LengthWithoutCrc = EepromLayout.UserBlockLength;

        public ushort Schema { get; set; }
        public string ProbeSerialNumber { get; set; } = "";
        public DateTime ProbeExpiryDate { get; set; } = DateTime.Now.AddYears(2);
        public ushort Crc16 { get; set; }
        public DateTime ProbeUsageDate { get; set; } = DateTime.MaxValue;

        public byte[] ToBytes()
        {
            byte[] data = new byte[EepromLayout.UserBlockLength];
            int offset = 0;

            byte[] schemaBytes = EndianHelper.ConvertUInt16ToBytesBigEndian(Schema);
            Array.Copy(schemaBytes, 0, data, offset, Math.Min(EepromLayout.UserSchemaSize, schemaBytes.Length));
            offset += EepromLayout.UserSchemaSize;

            byte[] probeSerialBytes = Encoding.ASCII.GetBytes(ProbeSerialNumber ?? "");
            Array.Copy(probeSerialBytes, 0, data, offset, Math.Min(EepromLayout.UserProbeSerialSize, probeSerialBytes.Length));
            offset += EepromLayout.UserProbeSerialSize;

            var pedBytes = DateTimeHelper.ConvertDateTimeToVendorBytes(ProbeExpiryDate);
            Array.Copy(pedBytes, 0, data, offset, EepromLayout.UserProbeExpirySize);

            Crc16 = CrcHelper.ComputeCrc16DallasMaxim(data, data.Length);

            byte[] withCrc = new byte[EepromLayout.UserProbeUsageDateOffset + EepromLayout.UserProbeUsageDateSize];
            Array.Copy(data, withCrc, data.Length);

            var crc16Bytes = BitConverter.GetBytes(Crc16);
            if (BitConverter.IsLittleEndian)
                Array.Reverse(crc16Bytes);
            Array.Copy(crc16Bytes, 0, withCrc, data.Length, EepromLayout.CrcSize);

            // 0xFF fill between CRC and ProbeUsageDate (row-alignment padding)
            for (int i = data.Length + EepromLayout.CrcSize; i < EepromLayout.UserProbeUsageDateOffset; i++)
                withCrc[i] = 0xFF;

            byte[] pudBytes = DateTimeHelper.ConvertDateTimeToVendorBytes(ProbeUsageDate);
            Array.Copy(pudBytes, 0, withCrc, EepromLayout.UserProbeUsageDateOffset, Math.Min(EepromLayout.UserProbeUsageDateSize, pudBytes.Length));

            return withCrc;
        }

        public bool ValidateCrc()
        {
            return Crc16 == CrcHelper.ComputeCrc16DallasMaximByPregenTable(ToBytes(), LengthWithoutCrc);
        }

        public void RecalculateCrc()
        {
            Crc16 = CrcHelper.ComputeCrc16DallasMaximByPregenTable(ToBytes(), LengthWithoutCrc);
        }
    }
}
