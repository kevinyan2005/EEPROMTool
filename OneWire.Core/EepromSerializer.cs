using System;
using System.Collections.Generic;
using System.Text;
using OneWire.Common;

namespace OneWire.Core
{
    internal class EepromSerializer
    {
        public EepromData Decode(byte[] raw, CrcCheckOptions crcCheckOptions = null)
        {
            if (raw == null || raw.Length < EepromLayout.TotalSize)
                throw new ArgumentException($"EEPROM image must be at least {EepromLayout.TotalSize} bytes.", nameof(raw));

            var data = new EepromData();

            // Identification block
            int offset = EepromLayout.IdBlockStart;
            data.Id.DataVersion   = EndianHelper.ReadUInt16FromBytesBigEndian(raw, offset + EepromLayout.IdVersionOffset);
            data.Id.DataId        = Encoding.ASCII.GetString(raw, offset + EepromLayout.IdDataIdOffset, EepromLayout.IdDataIdSize).TrimEnd('\0');
            data.Id.Model         = Encoding.ASCII.GetString(raw, offset + EepromLayout.IdModelOffset,  EepromLayout.IdModelSize).TrimEnd('\0');
            data.Id.SerialNumber  = Encoding.ASCII.GetString(raw, offset + EepromLayout.IdSerialOffset, EepromLayout.IdSerialSize).TrimEnd('\0');
            data.Id.Crc16         = EndianHelper.ReadUInt16FromBytesBigEndian(raw, offset + EepromLayout.IdCrcOffset);

            // Calibration block
            offset = EepromLayout.CalBlockStart;
            for (int i = 0; i < EepromLayout.CalGaugeFactorCount; i++)
                data.Calibration.GaugeFactors[i] = EndianHelper.ReadUInt32FromBytesWithWordSwap(raw, offset + i * EepromLayout.CalGaugeFactorSize);
            data.Calibration.ReferenceValue  = EndianHelper.ReadUInt32FromBytesWithWordSwap(raw, offset + EepromLayout.CalReferenceValueOffset);
            data.Calibration.ManufactureDate = DateTimeHelper.ReadVendorDateTimeOrNull(raw, offset + EepromLayout.CalManufactureDateOffset) ?? default;
            data.Calibration.ExpiryDate      = DateTimeHelper.ReadVendorDateTimeOrNull(raw, offset + EepromLayout.CalExpiryDateOffset)      ?? default;
            data.Calibration.GaugeType       = Encoding.ASCII.GetString(raw, offset + EepromLayout.CalGaugeTypeOffset, EepromLayout.CalGaugeTypeSize).TrimEnd('\0');
            data.Calibration.Crc16           = EndianHelper.ReadUInt16FromBytesBigEndian(raw, offset + EepromLayout.CalCrcOffset);

            // User block
            offset = EepromLayout.UserBlockStart;
            data.User.Schema                = EndianHelper.ReadUInt16FromBytesBigEndian(raw, offset + EepromLayout.UserSchemaOffset);
            data.User.ProbeSerialNumber     = Encoding.ASCII.GetString(raw, offset + EepromLayout.UserProbeSerialOffset, EepromLayout.UserProbeSerialSize).TrimEnd('\0', ' ');
            data.User.ProbeExpiryDate       = DateTimeHelper.ReadVendorDateTimeOrNull(raw, offset + EepromLayout.UserProbeExpiryOffset)         ?? default;
            data.User.Crc16                 = EndianHelper.ReadUInt16FromBytesBigEndian(raw, offset + EepromLayout.UserCrcOffset);
            data.User.ProbeUsageDate        = DateTimeHelper.ReadVendorDateTimeOrNull(raw, offset + EepromLayout.UserProbeUsageDateOffset)    ?? default;
            data.User.ProbeManufactureDate  = DateTimeHelper.ReadVendorDateTimeOrNull(raw, offset + EepromLayout.UserManufactureDateOffset)   ?? default;

            ValidateCrc(data, crcCheckOptions ?? new CrcCheckOptions());

            return data;
        }

        private static void ValidateCrc(EepromData data, CrcCheckOptions options)
        {
            var failures = new List<string>();

            if (options.CheckIdentification)
            {
                ushort idStored = data.Id.Crc16;
                if (!data.Id.ValidateCrc())
                    failures.Add($"Identification block (stored=0x{idStored:X4}, computed=0x{data.Id.Crc16:X4})");
            }

            if (options.CheckCalibration)
            {
                ushort calStored = data.Calibration.Crc16;
                if (!data.Calibration.ValidateCrc())
                    failures.Add($"Calibration block (stored=0x{calStored:X4}, computed=0x{data.Calibration.Crc16:X4})");
            }

            if (options.CheckUser)
            {
                ushort userStored = data.User.Crc16;
                if (!data.User.ValidateCrc())
                    failures.Add($"User block (stored=0x{userStored:X4}, computed=0x{data.User.Crc16:X4})");
            }

            // All fields are already parsed into `data` at this point, regardless of CRC outcome.
            // Attach it to the exception so callers can still use the parsed fields for the sections that failed.
            if (failures.Count > 0)
                throw new CrcValidationException(
                    $"CRC mismatch in: {string.Join(", ", failures)}.", data);
        }

        public byte[] Encode(EepromData data)
        {
            byte[] vendor = ByteArrayHelper.Concatenate(data.Id.ToBytes(), data.Calibration.ToBytes());
            byte[] full   = ByteArrayHelper.ConcatenateWithPadding(vendor, data.User.ToBytes());
            byte[] image  = new byte[EepromLayout.TotalSize];
            for (int i = 0; i < image.Length; i++) image[i] = 0xFF;
            Array.Copy(full, image, Math.Min(full.Length, EepromLayout.TotalSize));
            return image;
        }
    }
}
