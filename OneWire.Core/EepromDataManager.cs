using System;
using System.Diagnostics;
using System.Threading.Tasks;
using OneWire.Adapters;
using slf4net;

namespace OneWire.Core
{
    public class EepromDataManager : IEepromDataManager
    {
        private static ILogger Logger { get; } = LoggerFactory.GetLogger(nameof(EepromDataManager));

        private readonly EepromSerializer _serializer = new EepromSerializer();
        private readonly EepromFileService _fileService = new EepromFileService();

        private IOneWireAdapter _adapter;
        private bool _useOverdrive;

        public bool IsConnected => _adapter != null;

        public void Open(IOneWireAdapter adapter)
        {
            _adapter = adapter ?? throw new ArgumentNullException(nameof(adapter));
            _adapter.Connect();
            _adapter.OWReset();
            Logger.Info("Adapter opened.");
        }

        public void Close()
        {
            if (_adapter == null) return;
            _adapter.Reset();
            _adapter.Disconnect();
            _adapter = null;
            Logger.Info("Adapter closed.");
        }

        public void SetSpeed(bool useOverdrive)
        {
            _useOverdrive = useOverdrive;
            if (_adapter == null) return;
            if (useOverdrive) _adapter.EnterOverdrive();
            else _adapter.EnterStandard();
        }

        public Task<byte[]> ReadRawAsync(IProgress<int> progress = null)
        {
            EnsureConnected();
            return MeasureAsync(() => _adapter.ReadEntireMemoryAsync(_useOverdrive, progress), "Read EEPROM");
        }

        public async Task<byte[]> WriteAsync(EepromData data, WriteMode mode, byte eraseFillByte = 0x00, IProgress<int> progress = null)
        {
            EnsureConnected();
            var (address, imageBytes) = BuildImage(data, mode, eraseFillByte);
            await MeasureAsync(() => _adapter.WriteMemoryAsync(address, imageBytes, _useOverdrive, progress), $"Write EEPROM ({mode})");
            await Task.Delay(1000);
            return await _adapter.ReadEntireMemoryAsync(_useOverdrive);
        }

        public EepromData Decode(byte[] rawBytes, CrcCheckOptions crcCheckOptions = null) => _serializer.Decode(rawBytes, crcCheckOptions);
        public byte[] Encode(EepromData data) => _serializer.Encode(data);
        public EepromData LoadFromJson(string path) => _fileService.LoadFromJson(path);
        public void SaveToJson(EepromData data, string path) => _fileService.SaveToJson(data, path);
        public byte[] LoadRawHex(string path) => _fileService.LoadFromRawTxt(path);
        public void SaveRawHex(byte[] rawBytes, string path) => _fileService.SaveToRawTxt(rawBytes, path);

        private (ushort address, byte[] data) BuildImage(EepromData data, WriteMode mode, byte eraseFillByte)
        {
            switch (mode)
            {
                case WriteMode.Entire:
                    return (0, _serializer.Encode(data));

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
