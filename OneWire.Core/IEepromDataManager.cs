using System;
using System.Threading.Tasks;
using OneWire.Adapters;

namespace OneWire.Core
{
    public interface IEepromDataManager
    {
        bool IsConnected { get; }

        void Open(IOneWireAdapter adapter);
        void Close();
        void SetSpeed(bool useOverdrive);

        Task<byte[]> ReadRawAsync(IProgress<int> progress = null);
        Task<byte[]> WriteAsync(EepromData data, WriteMode mode, byte eraseFillByte = 0x00, IProgress<int> progress = null);

        EepromData Decode(byte[] rawBytes, CrcCheckOptions crcCheckOptions = null);
        byte[] Encode(EepromData data);

        EepromData LoadFromJson(string path);
        void SaveToJson(EepromData data, string path);
        byte[] LoadRawHex(string path);
        void SaveRawHex(byte[] rawBytes, string path);
    }
}
