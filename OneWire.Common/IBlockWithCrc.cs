using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OneWire.Common
{
    public interface IBlockWithCrc
    {
        ushort Crc16 { get; set; }
        bool ValidateCrc();
        void RecalculateCrc();
    }
}
