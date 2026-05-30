using System;
using System.Threading.Tasks;

namespace OneWire.Adapters
{
    public interface IOneWireAdapter
    {
        void Connect();
        void Disconnect();
        void Reset();
        bool OWReset();
        void EnterOverdrive();
        void EnterStandard();
        Task<byte[]> ReadEntireMemoryAsync(bool overdrive = false, IProgress<int>? progress = null);
        Task WriteMemoryAsync(ushort address, byte[] data, bool overdrive = false, IProgress<int>? progress = null);
    }
}
