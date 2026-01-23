using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OneWireController
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
