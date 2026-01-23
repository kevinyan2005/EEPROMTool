using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OneWire.Common
{
    public static class CrcHelper
    {
        /// <summary>
        /// Computes Dallas/Maxim CRC16.
        /// Polynomial: 0xA001
        /// Initial value: 0x0000
        /// </summary>
        public static ushort ComputeCrc16(byte[] data, int length)
        {
            ushort crc = 0;

            for (int i = 0; i < length; i++)
            {
                crc ^= (ushort)(data[i] & 0xFF);

                for (int j = 0; j < 8; j++)
                {
                    if ((crc & 0x0001) != 0)
                        crc = (ushort)((crc >> 1) ^ 0xA001);
                    else
                        crc >>= 1;
                }
            }

            return crc;
        }


        /// <summary>
        /// Calculate Maxim 1-Wire CRC8.
        /// Polynomial: x^8 + x^5 + x^4 + 1 (0x31 reversed -> 0x8C)
        /// Used for short data validation, typically used for ≤ 8 bytes (like ROM ID or command headers).
        /// </summary>
        public static byte ComputeCrc8(byte[] data, int offset = 0, int length = -1)
        {
            if (length < 0) length = data.Length - offset;

            byte crc = 0;
            for (int i = offset; i < offset + length; i++)
            {
                byte inbyte = data[i];
                for (int j = 0; j < 8; j++)
                {
                    byte mix = (byte)((crc ^ inbyte) & 0x01);
                    crc >>= 1;
                    if (mix != 0)
                        crc ^= 0x8C; // reversed polynomial
                    inbyte >>= 1;
                }
            }
            return crc;
        }
    }
}
