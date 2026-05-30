using System;

namespace OneWire.Common
{
    public static class EndianHelper
    {
        public static ushort ReadUInt16FromBytesBigEndian(byte[] data, int offset)
        {
            return (ushort)((data[offset] << 8) | data[offset + 1]);
        }

        public static void ConvertUInt16ToBytesBigEndian(ushort value, byte[] data, int offset)
        {
            data[offset]     = (byte)((value >> 8) & 0xFF);
            data[offset + 1] = (byte)(value & 0xFF);
        }

        public static byte[] ConvertUInt16ToBytesBigEndian(ushort value)
        {
            return new byte[]
            {
                (byte)((value >> 8) & 0xFF),
                (byte)(value & 0xFF)
            };
        }

        /// <summary>
        /// Reads 4 bytes from buffer with word-swap:
        /// byte[0,1,2,3] in EEPROM → logical (1,0,3,2) before BitConverter.
        /// </summary>
        public static uint ReadUInt32FromBytesWithWordSwap(byte[] data, int startAddress)
        {
            if (data.Length < startAddress + 4) throw new ArgumentException("Need 4 bytes.");

            byte[] swapped = new byte[4];
            swapped[0] = data[startAddress + 1];
            swapped[1] = data[startAddress + 0];
            swapped[2] = data[startAddress + 3];
            swapped[3] = data[startAddress + 2];

            return BitConverter.ToUInt32(swapped, 0);
        }

        /// <summary>
        /// Returns a new 4-byte array with word-swap encoding of value.
        /// </summary>
        public static byte[] ConvertUInt32ToBytesWithWordSwap(uint value)
        {
            byte[] bytes = BitConverter.GetBytes(value);
            return new byte[]
            {
                bytes[1], bytes[0],
                bytes[3], bytes[2]
            };
        }

        public static uint? ReadUInt32FromBytesOrNullWithWordSwap(byte[] data, int startAddress)
        {
            if (data == null || data.Length < startAddress + 4)
                throw new ArgumentException("Insufficient data to read 4 bytes at the specified address.");

            if (data[startAddress]     == 0xFF && data[startAddress + 1] == 0xFF &&
                data[startAddress + 2] == 0xFF && data[startAddress + 3] == 0xFF)
                return null;

            byte[] swapped = new byte[4];
            swapped[0] = data[startAddress + 1];
            swapped[1] = data[startAddress + 0];
            swapped[2] = data[startAddress + 3];
            swapped[3] = data[startAddress + 2];

            return BitConverter.ToUInt32(swapped, 0);
        }

        public static uint ReadUInt32FromBytesWithShuffle(byte[] data)
        {
            if (data.Length < 4) throw new ArgumentException("Need 4 bytes.");

            byte[] adjusted = new byte[] { data[2], data[3], data[0], data[1] };
            if (BitConverter.IsLittleEndian)
                Array.Reverse(adjusted);
            return BitConverter.ToUInt32(adjusted, 0);
        }
    }
}
