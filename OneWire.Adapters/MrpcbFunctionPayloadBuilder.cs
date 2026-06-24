using OneWire.Common;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OneWire.Adapters
{
    public static class MrpcbFunctionPayloadBuilder
    {
        public const ushort DefaultMrpcbFunctionAddress = 0x0000;
        public const int MaxMrpcbFunctionDataLength = 128;
        public const ushort FixedEepromReadAddress = 0x0000;
        public const int FixedEepromReadSize = 128;

        public static byte[] Build(
            MrpcbFunctionCode functionCode,
            ushort address = DefaultMrpcbFunctionAddress,
            byte[] data = null,
            int requestedSize = 0)
        {
            var payloadData = data ?? Array.Empty<byte>();
            if (payloadData.Length > MaxMrpcbFunctionDataLength)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(data),
                    $"MRPCB function data cannot exceed {MaxMrpcbFunctionDataLength} bytes.");
            }

            var functionSize = 0;

            switch (functionCode)
            {
                case MrpcbFunctionCode.Read_FOTS_EEPROM:
                case MrpcbFunctionCode.Read_RPD_EEPROM:
                case MrpcbFunctionCode.Read_Engine_EEPROM:
                    address = FixedEepromReadAddress;
                    functionSize = FixedEepromReadSize;
                    payloadData = Array.Empty<byte>();
                    break;
                case MrpcbFunctionCode.Write_FOTS_EEPROM:
                case MrpcbFunctionCode.Write_RPD_EEPROM:
                case MrpcbFunctionCode.Write_Engine_EEPROM:
                    if (requestedSize > 0 && requestedSize < payloadData.Length)
                    {
                        payloadData = payloadData.Take(requestedSize).ToArray();
                    }

                    functionSize = payloadData.Length;
                    break;
            }

            var result = new byte[3 + payloadData.Length];
            result[0] = (byte)functionSize;
            result[1] = (byte)(address & 0xFF);
            result[2] = (byte)((address >> 8) & 0xFF);

            if (payloadData.Length > 0)
            {
                Buffer.BlockCopy(payloadData, 0, result, 3, payloadData.Length);
            }

            return result;
        }

        public static bool IsEepromReadFunction(MrpcbFunctionCode functionCode)
        {
            return functionCode == MrpcbFunctionCode.Read_FOTS_EEPROM
                   || functionCode == MrpcbFunctionCode.Read_RPD_EEPROM
                   || functionCode == MrpcbFunctionCode.Read_Engine_EEPROM;
        }

        public static bool IsEepromWriteFunction(MrpcbFunctionCode functionCode)
        {
            return functionCode == MrpcbFunctionCode.Write_FOTS_EEPROM
                   || functionCode == MrpcbFunctionCode.Write_RPD_EEPROM
                   || functionCode == MrpcbFunctionCode.Write_Engine_EEPROM;
        }

    }
}
