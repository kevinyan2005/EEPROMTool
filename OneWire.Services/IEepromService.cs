using System;
using System.Threading.Tasks;
using OneWire.Common;

namespace OneWire.Services
{
    public interface IEepromService
    {
        bool IsConnected { get; }
        void Connect(AdapterType adapterType, string port);
        void Disconnect();
        void SetSpeed(bool useOverdrive);
        Task<byte[]> ReadAsync(IProgress<int> progress = null);
        /// <summary>
        /// Writes to EEPROM according to <paramref name="mode"/>, then reads back and returns fresh bytes.
        /// </summary>
        Task<byte[]> WriteAsync(EepromData data, WriteMode mode, byte eraseFillByte = 0x00, IProgress<int> progress = null);
    }
}
