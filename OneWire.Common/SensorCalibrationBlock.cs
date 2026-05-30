using System;
using System.Text;

namespace OneWire.Common
{
    public class SensorCalibrationBlock : IBlockWithCrc
    {
        public const int LengthWithoutCrc = EepromLayout.CalBlockLength;

        public uint[] GaugeFactors { get; set; } = new uint[EepromLayout.CalGaugeFactorCount];
        public uint ReferenceValue { get; set; } = uint.MaxValue; // 4 bytes — fixed at 0xFFFFFFFF, not user-editable
        public DateTime ManufactureDate { get; set; }  // 8 bytes
        public DateTime ExpiryDate { get; set; }       // 8 bytes
        public string GaugeType { get; set; }          // 2 bytes
        public ushort Crc16 { get; set; }              // 2 bytes

        public byte[] ToBytes()
        {
            byte[] data = new byte[EepromLayout.CalBlockLength];
            int offset = 0;

            for (int i = 0; i < EepromLayout.CalGaugeFactorCount; i++)
            {
                var gfBytes = ByteHelper.ConvertUInt32ToBytesWithWordSwap(GaugeFactors[i]);
                Array.Copy(gfBytes, 0, data, offset, EepromLayout.CalGaugeFactorSize);
                offset += EepromLayout.CalGaugeFactorSize;
            }

            var refBytes = ByteHelper.ConvertUInt32ToBytesWithWordSwap(ReferenceValue);
            Array.Copy(refBytes, 0, data, offset, EepromLayout.CalReferenceValueSize);
            offset += EepromLayout.CalReferenceValueSize;

            byte[] mfgDateBytes = ByteHelper.ConvertDateTimeToVendorBytes(ManufactureDate);
            Array.Copy(mfgDateBytes, 0, data, offset, EepromLayout.CalManufactureDateSize);
            offset += EepromLayout.CalManufactureDateSize;

            byte[] edBytes = ByteHelper.ConvertDateTimeToVendorBytes(ExpiryDate);
            Array.Copy(edBytes, 0, data, offset, EepromLayout.CalExpiryDateSize);
            offset += EepromLayout.CalExpiryDateSize;

            byte[] gaugeBytes = Encoding.ASCII.GetBytes(GaugeType ?? "");
            Array.Copy(gaugeBytes, 0, data, offset, Math.Min(EepromLayout.CalGaugeTypeSize, gaugeBytes.Length));

            Crc16 = CrcHelper.ComputeCrc16(data, data.Length);

            byte[] withCrc = new byte[EepromLayout.CalBlockLength + EepromLayout.CrcSize];
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
