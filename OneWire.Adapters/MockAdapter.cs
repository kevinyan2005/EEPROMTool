using System;
using System.Threading.Tasks;
using slf4net;

namespace OneWire.Adapters
{
    /// <summary>
    /// No-hardware adapter for offline testing. Returns a zeroed 128-byte EEPROM image.
    /// </summary>
    public class MockAdapter : IOneWireAdapter
    {
        private static ILogger Logger { get; } = LoggerFactory.GetLogger(nameof(MockAdapter));

        private byte[] _memory = new byte[128];

        public void Connect() => Logger.Info("MockAdapter: Connect");
        public void Disconnect() => Logger.Info("MockAdapter: Disconnect");
        public void Reset() => Logger.Info("MockAdapter: Reset");
        public bool OWReset() => true;
        public void EnterOverdrive() => Logger.Info("MockAdapter: EnterOverdrive");
        public void EnterStandard() => Logger.Info("MockAdapter: EnterStandard");

        public async Task<byte[]> ReadEntireMemoryAsync(bool overdrive = false, IProgress<int>? progress = null)
        {
            Logger.Info("MockAdapter: ReadEntireMemoryAsync");
            for (int i = 0; i <= 100; i += 25)
            {
                await Task.Delay(20);
                progress?.Report(i);
            }
            return (byte[])_memory.Clone();
        }

        public async Task WriteMemoryAsync(ushort address, byte[] data, bool overdrive = false, IProgress<int>? progress = null)
        {
            Logger.Info($"MockAdapter: WriteMemoryAsync address=0x{address:X4} length={data.Length}");
            Array.Copy(data, 0, _memory, address, Math.Min(data.Length, _memory.Length - address));
            for (int i = 0; i <= 100; i += 25)
            {
                await Task.Delay(10);
                progress?.Report(i);
            }
        }
    }
}
