using System.Collections.Generic;
using System.Linq;

namespace OneWire.Adapters
{
    public static class ByteArrayExtensions
    {
        public static IEnumerable<byte> PadRight(this IEnumerable<byte> source, int size)
        {
            var list = source.ToList();
            while (list.Count < size)
                list.Add(0);
            return list;
        }
    }
}
