using System;
using System.Diagnostics;
using System.Threading.Tasks;
using OneWire.Common;
using OneWireController;
using slf4net;

namespace OneWire.Services
{
    public class EepromService : IEepromService
    {
        private static ILogger Logger { get; } = LoggerFactory.GetLogger(nameof(EepromService));

        private readonly IEepromSerializer _eepromSerializer;
        private IOneWireAdapter _adapter;

        public EepromService(IEepromSerializer eepromSerializer)
        {
            _eepromSerializer = eepromSerializer;
        }

        public bool IsConnected => _adapter != null;

        public void Connect(AdapterType adapterType, string port)
        {
            _adapter = OneWireAdapterFactory.Create(adapterType, port);
            _adapter.Connect();
            _adapter.OWReset();
            Logger.Info($"Connected via {adapterType} on {port}");
        }

        public void Disconnect()
        {
            if (_adapter == null) return;
            _adapter.Reset();
            _adapter.Disconnect();
            _adapter = null;
            Logger.Info("Disconnected from adapter");
        }

        public void SetSpeed(bool useOverdrive)
        {
            if (_adapter == null) return;
            if (useOverdrive) _adapter.EnterOverdrive();
            else _adapter.EnterStandard();
        }

        public Task<byte[]> ReadAsync(IProgress<int> progress = null)
        {
            EnsureConnected();
            return MeasureAsync(
                () => _adapter.ReadEntireMemoryAsync(overdrive: false, progress),
                "Read Entire EEPROM");
        }

        public async Task<byte[]> WriteAsync(EepromData data, WriteMode mode, byte eraseFillByte = 0x00, IProgress<int> progress = null)
        {
            EnsureConnected();
            var (address, imageBytes) = BuildImage(data, mode, eraseFillByte);
            await MeasureAsync(
                () => _adapter.WriteMemoryAsync(address, imageBytes, overdrive: false, progress),
                $"Write EEPROM ({mode})");
            return await _adapter.ReadEntireMemoryAsync(overdrive: false);
        }

        private (ushort address, byte[] data) BuildImage(EepromData data, WriteMode mode, byte eraseFillByte)
        {
            switch (mode)
            {
                case WriteMode.Entire:
                    return (0, _eepromSerializer.Encode(data));

                case WriteMode.UserDataOnly:
                {
                    const int userAreaSize = EepromLayout.TotalSize - EepromLayout.UserBlockStart;
                    byte[] userData = data.User.ToBytes();
                    byte[] area = new byte[userAreaSize];
                    for (int i = 0; i < area.Length; i++) area[i] = 0xFF;
                    Array.Copy(userData, area, Math.Min(userData.Length, userAreaSize));
                    return ((ushort)EepromLayout.UserBlockStart, area);
                }

                case WriteMode.Erase:
                {
                    byte[] eraseImage = new byte[EepromLayout.TotalSize];
                    for (int i = 0; i < eraseImage.Length; i++) eraseImage[i] = eraseFillByte;
                    return (0, eraseImage);
                }

                default:
                    throw new ArgumentOutOfRangeException(nameof(mode), mode, null);
            }
        }

        private void EnsureConnected()
        {
            if (_adapter == null)
                throw new InvalidOperationException("Not connected to a 1-Wire adapter.");
        }

        private static async Task<T> MeasureAsync<T>(Func<Task<T>> operation, string name)
        {
            Logger.Info($"{name} started...");
            var sw = Stopwatch.StartNew();
            try
            {
                var result = await operation();
                sw.Stop();
                Logger.Info($"{name} completed in {sw.ElapsedMilliseconds:N0} ms.");
                return result;
            }
            catch (Exception ex)
            {
                sw.Stop();
                Logger.Error(ex, $"{name} failed after {sw.ElapsedMilliseconds:N0} ms.");
                throw;
            }
        }

        private static async Task MeasureAsync(Func<Task> operation, string name)
        {
            Logger.Info($"{name} started...");
            var sw = Stopwatch.StartNew();
            try
            {
                await operation();
                sw.Stop();
                Logger.Info($"{name} completed in {sw.ElapsedMilliseconds:N0} ms.");
            }
            catch (Exception ex)
            {
                sw.Stop();
                Logger.Error(ex, $"{name} failed after {sw.ElapsedMilliseconds:N0} ms.");
                throw;
            }
        }
    }
}
