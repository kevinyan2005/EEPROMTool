using System;
using System.Collections.Generic;

namespace OneWire.Common
{
    public static class ByteArrayHelper
    {
        public static byte[] Concatenate(params byte[][] blocks)
        {
            int totalSize = 0;
            foreach (var block in blocks)
                if (block != null) totalSize += block.Length;

            byte[] result = new byte[totalSize];
            int offset = 0;
            foreach (var block in blocks)
            {
                if (block != null)
                {
                    Buffer.BlockCopy(block, 0, result, offset, block.Length);
                    offset += block.Length;
                }
            }
            return result;
        }

        // Aligns each block's start to a multiple of 8, filling gaps with 0xFF.
        public static byte[] ConcatenateWithPadding(params byte[][] blocks)
        {
            var result = new List<byte>();
            foreach (var block in blocks)
            {
                int paddingSize = (8 - (result.Count % 8)) % 8;
                for (int i = 0; i < paddingSize; i++)
                    result.Add(0xFF);
                result.AddRange(block);
            }
            return result.ToArray();
        }
    }
}
