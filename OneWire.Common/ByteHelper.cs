using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OneWire.Common
{
    public static class ByteHelper
    {
        //private static ILogger Logger { get; } = LoggerFactory.GetLogger(nameof(ByteHelper));

        public static byte[] ConcatenateWithPadding(params byte[][] blocks)
        {
            List<byte> result = new List<byte>();
            foreach (var block in blocks)
            {
                // Align start address to multiple of 8
                int paddingSize = (8 - (result.Count % 8)) % 8;				
                //if (paddingSize > 0)
                //    result.AddRange(new byte[paddingSize]);
				
                if (paddingSize > 0)
                {
                    // 2. Create padding and fill it manually with 0xFF
                    byte[] padding = new byte[paddingSize];
                    for (int i = 0; i < paddingSize; i++)
                    {
                        padding[i] = 0xFF;
                    }
                    result.AddRange(padding);
                }

                // Add the block
                result.AddRange(block);
            }

            return result.ToArray();
        }

        public static byte[] Concatenate(params byte[][] blocks)
        {
            // Calculate total size first for better performance (avoids multiple reallocations)
            int totalSize = 0;
            foreach (var block in blocks)
            {
                if (block != null) totalSize += block.Length;
            }

            byte[] result = new byte[totalSize];
            int currentOffset = 0;

            foreach (var block in blocks)
            {
                if (block != null)
                {
                    Buffer.BlockCopy(block, 0, result, currentOffset, block.Length);
                    currentOffset += block.Length;
                }
            }

            return result;
        }


        public static ushort ReadUInt16FromBytesBigEndian(byte[] data, int offset)
        {
            return (ushort)((data[offset] << 8) | data[offset + 1]);
        }

        public static void ConvertUInt16ToBytesBigEndian(ushort value, byte[] data, int offset)
        {
            // High byte (MSB) first
            data[offset] = (byte)((value >> 8) & 0xFF);

            // Low byte (LSB) second
            data[offset + 1] = (byte)(value & 0xFF);
        }

        /// <summary>
        /// Converts a UInt16 to a 2-byte Big-Endian array.
        /// </summary>
        public static byte[] ConvertUInt16ToBytesBigEndian(ushort value)
        {
            return new byte[]
            {
                (byte)((value >> 8) & 0xFF), // High byte (MSB)
                (byte)(value & 0xFF)         // Low byte (LSB)
            };
        }

        /// <summary>
        /// Reads 4 bytes from EEPROM buffer and converts to unsigned integer with byte swapping.
        /// Swap pattern:
        /// New Byte 0 = Old Byte 2
        /// New Byte 1 = Old Byte 3
        /// New Byte 2 = Old Byte 0
        /// New Byte 3 = Old Byte 1
        /// </summary>
        public static uint ReadUInt32FromBytesWithWordSwap(byte[] data, int startAddress)
        {
            if (data.Length < 4) throw new ArgumentException("Need 4 bytes.");


            byte[] swappedBytes = new byte[4];
            swappedBytes[0] = data[startAddress + 1];  // byte 1 -> byte 0
            swappedBytes[1] = data[startAddress + 0];  // byte 0 -> byte 1
            swappedBytes[2] = data[startAddress + 3];  // byte 3 -> byte 2
            swappedBytes[3] = data[startAddress + 2];  // byte 2 -> byte 3

            // Convert to UInt32
            return BitConverter.ToUInt32(swappedBytes, 0);
        }

        public static void ConvertUint32ToBytesWithWordSwap(uint value, byte[] data, int startAddress)
        {
            if (data.Length - startAddress < 4)
                throw new ArgumentException("Insufficient space in target array.");

            // 1. Get the raw bytes of the UInt32 (Little-Endian: [B0, B1, B2, B3])
            byte[] rawBytes = BitConverter.GetBytes(value);

            // 2. Apply Word Swap and write to the target array
            // Word 1: Swap B0 and B1
            data[startAddress + 0] = rawBytes[1]; // B1 -> index 0
            data[startAddress + 1] = rawBytes[0]; // B0 -> index 1

            // Word 2: Swap B2 and B3
            data[startAddress + 2] = rawBytes[3]; // B3 -> index 2
            data[startAddress + 3] = rawBytes[2]; // B2 -> index 3
        }

        public static byte[] ConvertUint32ToBytesWithWordSwap(uint value)
        {
            return new byte[]
            {
                (byte)((value >> 8) & 0xFF),  // High byte of low word
                (byte)(value & 0xFF),         // Low byte of low word
                (byte)((value >> 24) & 0xFF), // High byte of high word
                (byte)((value >> 16) & 0xFF)  // Low byte of high word
            };
        }

        public static uint? ReadUInt32FromBytesOrNullWithWordSwap(byte[] data, int startAddress)
        {
            // Ensure we have enough data to read 4 bytes from the startAddress
            if (data == null || data.Length < startAddress + 4)
                throw new ArgumentException("Insufficient data to read 4 bytes at the specified address.");

            // Check if the input is FF FF FF FF
            if (data[startAddress] == 0xFF && data[startAddress + 1] == 0xFF &&
                data[startAddress + 2] == 0xFF && data[startAddress + 3] == 0xFF)
            {
                return null;
            }

            byte[] swappedBytes = new byte[4];
            swappedBytes[0] = data[startAddress + 1];  // byte 1 -> byte 0
            swappedBytes[1] = data[startAddress + 0];  // byte 0 -> byte 1
            swappedBytes[2] = data[startAddress + 3];  // byte 3 -> byte 2
            swappedBytes[3] = data[startAddress + 2];  // byte 2 -> byte 3

            // Convert to UInt32
            return BitConverter.ToUInt32(swappedBytes, 0);
        }

        public static uint ReadUInt32FromBytesWithShuffle(byte[] data)
        {
            if (data.Length < 4) throw new ArgumentException("Need 4 bytes.");


            // 1.Bytes shuffle: (0,1,2,3) -> (2,3,0,1).
            byte[] adjusted = new byte[] { data[2], data[3], data[0], data[1] };

            // 2. Interpret as Big Endian to get 124,889
            if (BitConverter.IsLittleEndian)
            {
                Array.Reverse(adjusted); // Flip to match Big Endian math
            }
            return BitConverter.ToUInt32(adjusted, 0);

        }

        /// <summary>
        /// Converts an unsigned integer into 4-bytes format with reversed byte order.
        /// </summary>
        public static byte[] ConvertUInt32ToBytesWithWordSwap(uint value)
        {
            byte[] bytes = BitConverter.GetBytes(value);

            // Swap bytes within each 16-bit word: (0↔1, 2↔3)
            byte[] swappedBytes = new byte[4];
            swappedBytes[0] = bytes[1];  // byte 1 -> byte 0
            swappedBytes[1] = bytes[0];  // byte 0 -> byte 1
            swappedBytes[2] = bytes[3];  // byte 3 -> byte 2
            swappedBytes[3] = bytes[2];  // byte 2 -> byte 3

            return swappedBytes;
        }

        //public static byte[] ConvertUInt32ToBytesWithShuffle(uint value)
        //{
        //    byte[] bytes = BitConverter.GetBytes(value);

        //    // Swap bytes within each 16-bit word: (0↔1, 2↔3)
        //    byte[] swappedBytes = new byte[4];
        //    swappedBytes[0] = bytes[1];  // byte 1 -> byte 0
        //    swappedBytes[1] = bytes[0];  // byte 0 -> byte 1
        //    swappedBytes[2] = bytes[3];  // byte 3 -> byte 2
        //    swappedBytes[3] = bytes[2];  // byte 2 -> byte 3

        //    return swappedBytes;
        //}

        // BCD stands for Binary-coded Decimal
        private static int BcdToInt(byte b)
        {
            int hi = (b >> 4) & 0x0F;
            int lo = b & 0x0F;

            // Valid BCD nibbles are 0..9
            if (hi > 9 || lo > 9) return -1;
            return hi * 10 + lo;
        }

        private static byte IntToBcd(int value)
        {
            // Example: 24 -> (2 << 4) | 4 -> 0x24
            return (byte)(((value / 10) << 4) | (value % 10));
        }

        // Vendor datetime (BCD): YY_hi YY_lo MM DD HH mm ss reserved
        // Example: 20 24 11 20 01 05 03 00 => 2024-11-20 01:05:03
        // Unset example: DD D0 D0 D0 D0 D0 D0 00 => null
        public static DateTime? ReadVendorDateTimeOrNull(byte[] data, int offset, DateTimeKind kind = DateTimeKind.Utc)
        {
            if (data == null) throw new ArgumentNullException(nameof(data));
            if (offset < 0 || offset + 8 > data.Length) throw new ArgumentOutOfRangeException(nameof(offset));

            byte b0 = data[offset + 0];
            byte b1 = data[offset + 1];
            byte b2 = data[offset + 2];
            byte b3 = data[offset + 3];
            byte b4 = data[offset + 4];
            byte b5 = data[offset + 5];
            byte b6 = data[offset + 6];
            // byte b7 = data[offset + 7]; // reserved

            // Detect vendor "unset" (your sample pattern)
            bool looksUnset =
                b1 == 0xD0 &&
                b2 == 0xD0 &&
                b3 == 0xD0 &&
                b4 == 0xD0 &&
                b5 == 0xD0 &&
                b6 == 0xD0;

            if (looksUnset) return null;

            int yyHi = BcdToInt(b0);
            int yyLo = BcdToInt(b1);
            int month = BcdToInt(b2);
            int day = BcdToInt(b3);
            int hour = BcdToInt(b4);
            int minute = BcdToInt(b5);
            int second = BcdToInt(b6);

            // Any non-BCD byte => invalid/unset
            if (yyHi < 0 || yyLo < 0 || month < 0 || day < 0 || hour < 0 || minute < 0 || second < 0)
                return null;

            int year = yyHi * 100 + yyLo;

            // Range checks
            if (year < 1 || year > 9999) return null;
            if (month < 1 || month > 12) return null;
            if (day < 1 || day > 31) return null;
            if (hour > 23) return null;
            if (minute > 59) return null;
            if (second > 59) return null;

            try
            {
                return new DateTime(year, month, day, hour, minute, second, kind);
            }
            catch
            {
                // invalid day-of-month etc.
                return null;
            }
        }

        public static byte[] ConvertDateTimeToVendorBytes(DateTime? dateTime)
        {
            byte[] data = new byte[8];

            // If null, fill with the vendor "unset" pattern (D0 D0 D0...)
            if (!dateTime.HasValue)
            {
                data[0] = 0x00; // yyHi remains 0 or whatever your vendor expects for null
                for (int i = 1; i < 7; i++) data[i] = 0xD0;
                data[7] = 0x00; // Reserved
                return data;
            }

            DateTime dt = dateTime.Value;

            // Split year (e.g., 2024 -> 20 and 24)
            int yyHi = dt.Year / 100;
            int yyLo = dt.Year % 100;

            // Convert components to BCD
            data[0] = IntToBcd(yyHi);
            data[1] = IntToBcd(yyLo);
            data[2] = IntToBcd(dt.Month);
            data[3] = IntToBcd(dt.Day);
            data[4] = IntToBcd(dt.Hour);
            data[5] = IntToBcd(dt.Minute);
            data[6] = IntToBcd(dt.Second);
            data[7] = 0x00; // Reserved/Padding

            return data;
        }


        public static DateTime? ReadDateTime(byte[] eeprom, int offset)
        {
            if (!TryReadDateTimeFromTicks(eeprom, offset, out var dt))
            {
                //long raw = (eeprom != null && offset >= 0 && offset + 18 + 8 <= eeprom.Length)
                //    ? BitConverter.ToInt64(eeprom, offset + 18)
                //    : 0;

                //Invoke($"Invalid expiry ticks: {raw} at offset {offset + 18}");
                return null;
            }
            return dt;
        }

        public static bool TryReadDateTimeFromTicks(byte[] eeprom, int offset, out DateTime expiryDate)
        {
            expiryDate = default;

            if (eeprom == null) return false;
            if (offset < 0) return false;

            long ticks = BitConverter.ToInt64(eeprom, offset);

            // Common sentinel/uninitialized patterns
            if (ticks == 0 || ticks == long.MinValue || ticks == long.MaxValue)
                return false;

            // Range check before constructing DateTime
            if (ticks < DateTime.MinValue.Ticks || ticks > DateTime.MaxValue.Ticks)
                return false;

            expiryDate = new DateTime(ticks, DateTimeKind.Utc);
            return true;
        }
    }
}
