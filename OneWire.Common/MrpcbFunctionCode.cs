using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OneWire.Common
{
    public enum MrpcbFunctionCode
    {
        No_Action = 0,
        Read_FOTS_EEPROM,
        Read_RPD_EEPROM,
        Read_Engine_EEPROM,
        Write_FOTS_EEPROM,
        Write_RPD_EEPROM,
        Write_Engine_EEPROM,
        Reset_MRPCB,
    }
}
