using OneWire.Common;

namespace OneWire.Services
{
    public interface IEepromFileService
    {
        EepromData LoadFromJson(string path);
        void SaveToJson(EepromData data, string path);
        byte[] LoadFromRawTxt(string path);
        void SaveToRawTxt(byte[] data, string path);
        string FormatHexAscii(byte[] data);
        string FormatHexRaw(byte[] data);
    }
}
