using System.ComponentModel;

namespace OneWire.Common
{
    public enum AdapterType
    {
        [Description("DS9490 (USB)")]
        DS9490,
        [Description("Haemonetics")]
        DCT,
        [Description("MRPCB - Serial Port")]
        MRPCB,
        [Description("HCB - Serial Port")]
        HCB,
        [Description("Mock (Offline)")]
        Mock,
    }
}
