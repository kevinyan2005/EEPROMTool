using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OneWire.Common
{
    public static class ByteHelper
    {

        public static byte[] ConcatenateWithPadding(params byte[][] blocks)
        {
            List<byte> result = new List<byte>();
            foreach (var block in blocks)
            {
                // Align start address to multiple of 8
                int padding = (8 - (result.Count % 8)) % 8;
                if (padding > 0)
                    result.AddRange(new byte[padding]);

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
    }
}
