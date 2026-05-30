using System;
using System.Text;
using OneWire.Common;

namespace OneWire.Services
{
    public class EepromSerializer : IEepromSerializer
    {
        public EepromData Decode(byte[] raw)
        {
            if (raw == null || raw.Length < EepromLayout.TotalSize)
                throw new ArgumentException($"EEPROM image must be at least {EepromLayout.TotalSize} bytes.", nameof(raw));

            var data = new EepromData();

            // Identification block
            int offset = EepromLayout.IdBlockStart;
            data.Id.DataVersion   = ByteHelper.ReadUInt16FromBytesBigEndian(raw, offset + EepromLayout.IdVersionOffset);
            data.Id.DataId        = Encoding.ASCII.GetString(raw, offset + EepromLayout.IdDataIdOffset, EepromLayout.IdDataIdSize).TrimEnd('\0');
            data.Id.Model         = Encoding.ASCII.GetString(raw, offset + EepromLayout.IdModelOffset,  EepromLayout.IdModelSize).TrimEnd('\0');
            data.Id.SerialNumber  = Encoding.ASCII.GetString(raw, offset + EepromLayout.IdSerialOffset, EepromLayout.IdSerialSize).TrimEnd('\0');
            data.Id.Crc16         = ByteHelper.ReadUInt16FromBytesBigEndian(raw, offset + EepromLayout.IdCrcOffset);

            // Calibration block
            offset = EepromLayout.CalBlockStart;
            for (int i = 0; i < EepromLayout.CalGaugeFactorCount; i++)
                data.Calibration.GaugeFactors[i] = ByteHelper.ReadUInt32FromBytesWithWordSwap(raw, offset + i * EepromLayout.CalGaugeFactorSize);
            data.Calibration.ReferenceValue  = ByteHelper.ReadUInt32FromBytesWithWordSwap(raw, offset + EepromLayout.CalReferenceValueOffset);
            data.Calibration.ManufactureDate = ByteHelper.ReadVendorDateTimeOrNull(raw, offset + EepromLayout.CalManufactureDateOffset) ?? default;
            data.Calibration.ExpiryDate      = ByteHelper.ReadVendorDateTimeOrNull(raw, offset + EepromLayout.CalExpiryDateOffset)      ?? default;
            data.Calibration.GaugeType       = Encoding.ASCII.GetString(raw, offset + EepromLayout.CalGaugeTypeOffset, EepromLayout.CalGaugeTypeSize).TrimEnd('\0');
            data.Calibration.Crc16           = ByteHelper.ReadUInt16FromBytesBigEndian(raw, offset + EepromLayout.CalCrcOffset);

            // User block
            offset = EepromLayout.UserBlockStart;
            data.User.Schema            = ByteHelper.ReadUInt16FromBytesBigEndian(raw, offset + EepromLayout.UserSchemaOffset);
            data.User.ProbeSerialNumber = Encoding.ASCII.GetString(raw, offset + EepromLayout.UserProbeSerialOffset, EepromLayout.UserProbeSerialSize).TrimEnd('\0');
            data.User.ProbeExpiryDate   = ByteHelper.ReadVendorDateTimeOrNull(raw, offset + EepromLayout.UserProbeExpiryOffset)      ?? default;
            data.User.Crc16             = ByteHelper.ReadUInt16FromBytesBigEndian(raw, offset + EepromLayout.UserCrcOffset);
            data.User.ProbeUsageDate    = ByteHelper.ReadVendorDateTimeOrNull(raw, offset + EepromLayout.UserProbeUsageDateOffset) ?? default;

            return data;
        }

        public byte[] Encode(EepromData data)
        {
            byte[] vendor = ByteHelper.Concatenate(data.Id.ToBytes(), data.Calibration.ToBytes());
            byte[] full   = ByteHelper.ConcatenateWithPadding(vendor, data.User.ToBytes());
            byte[] image  = new byte[EepromLayout.TotalSize];
            for (int i = 0; i < image.Length; i++) image[i] = 0xFF;
            Array.Copy(full, image, Math.Min(full.Length, EepromLayout.TotalSize));
            return image;
        }
    }
}
