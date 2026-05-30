using System.ComponentModel;

namespace OneWire.Services
{
    public enum WriteMode
    {
        [Description("Entire EEPROM")]
        Entire,
        [Description("User Data Only")]
        UserDataOnly,
        [Description("Erase EEPROM")]
        Erase
    }
}
