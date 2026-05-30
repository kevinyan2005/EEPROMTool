using System;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DalSemi.OneWire;
using DalSemi.OneWire.Adapter;
using slf4net;

namespace OneWire.Adapters
{
    public class DS9490Adapter : IOneWireAdapter
    {
        private static ILogger Logger { get; } = LoggerFactory.GetLogger(nameof(DS9490Adapter));

        private readonly PortAdapter _adapter;

        // DS2431 Command Constants
        private const byte CMD_SKIP_ROM = 0xCC;
        private const byte CMD_OVERDRIVE_SKIP_ROM = 0x3C;
        private const byte CMD_READ_MEMORY = 0xF0;
        private const byte CMD_WRITE_SCRATCHPAD = 0x0F;
        private const byte CMD_READ_SCRATCHPAD = 0xAA;
        private const byte CMD_COPY_SCRATCHPAD = 0x55;

        private const int PageSize = 32;

        public byte[] Rom { get; }

        public DS9490Adapter(string port)
        {
            try
            {
                _adapter = AccessProvider.GetAdapter("{DS9490}", port);
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Failed to initialize 1-Wire adapter. Is the DS9490 connected?");
                throw new InvalidOperationException("1-Wire adapter not available. Connect the USB adapter and try again.", ex);
            }
        }

        public void Connect()
        {
            Logger.Info("Connecting to 1-Wire adapter and searching for the devices...");
            _adapter.BeginExclusive(true);

            _adapter.SetSearchAllDevices();
            _adapter.TargetAllFamilies();
            _adapter.Speed = OWSpeed.SPEED_REGULAR;

            byte[] address = new byte[8];
            if (_adapter.GetFirstDevice(address, 0))
            {
                do
                {
                    Logger.Info($"1-Wire Device Address: {Reverse1WireHexAddress(address)}");
                }
                while (_adapter.GetNextDevice(address, 0));
            }
            _adapter.EndExclusive();
            Logger.Info("Connection established.");
        }

        public void Disconnect()
        {
            Logger.Info("Disconnecting from 1-Wire adapter...");
            _adapter.Dispose();
            Logger.Info("Disconnected.");
        }

        public void Reset()
        {
            _adapter.Reset();
        }

        public void SkipRom()
        {
            if (!OWReset()) return;
            _adapter.PutByte(CMD_SKIP_ROM);
        }

        public void OverdriveSkipRom()
        {
            if (!OWReset()) return;
            _adapter.PutByte(CMD_OVERDRIVE_SKIP_ROM);
        }

        public bool OWReset()
        {
            OWResetResult rslt;

            try
            {
                rslt = _adapter.Reset();
                if ((rslt == OWResetResult.RESET_PRESENCE) || (rslt == OWResetResult.RESET_ALARM))
                {
                    return true;
                }

                return false;
            }
            catch (Exception ex)
            {
                Logger.Error(ex.Message);
                return false;
            }
        }

        public void EnterOverdrive()
        {
            _adapter.Reset();
            _adapter.PutByte(CMD_OVERDRIVE_SKIP_ROM);
            _adapter.Speed = OWSpeed.SPEED_OVERDRIVE;
        }

        public void EnterStandard()
        {
            _adapter.Speed = OWSpeed.SPEED_REGULAR;
            _adapter.Reset();
            _adapter.PutByte(CMD_SKIP_ROM);
        }

        public Task<byte[]> ReadMemoryAsync(int address, int length, bool overdrive = false) =>
            Task.Run(() => ReadMemoryInternal(address, length, overdrive));

        public async Task<byte[]> ReadMemoryAsync(int address, int length, bool overdrive = false, IProgress<int>? progress = null)
        {
            byte[] memory = new byte[length];
            int blockSize = 8;

            for (int i = 0; i < memory.Length; i += blockSize)
            {
                Logger.Debug($"Reading 8 bytes from address: {i} ");
                var bytesRead = await Task.Run(() => ReadMemoryInternal(i, blockSize, false));
                Array.Copy(bytesRead, 0, memory, i, bytesRead.Length);
                progress?.Report((i + blockSize) * 100 / memory.Length);
            }

            return memory;
        }

        public byte[] ReadMemory(int address, int length) => ReadMemoryInternal(address, length, false);

        public byte[] ReadMemoryOverdrive(int address, int length) => ReadMemoryInternal(address, length, true);

        private byte[] ReadMemoryInternal(int address, int length, bool overdrive)
        {
            if (overdrive) OverdriveSkipRom();
            else SkipRom();

            _adapter.PutByte(CMD_READ_MEMORY);
            _adapter.PutByte((byte)(address & 0xFF));
            _adapter.PutByte((byte)((address >> 8) & 0xFF));

            return Enumerable.Range(0, length)
                .Select(_ => (byte)_adapter.GetByte())
                .ToArray();
        }

        public async Task<byte[]> ReadEntireMemoryAsync(bool overdrive = false, IProgress<int>? progress = null)
        {
            byte[] memory = new byte[128];

            for (int i = 0; i < 128; i += PageSize)
            {
                Logger.Debug($"Reading page(32 bytes) from address: {i} ");
                var bytesRead = await Task.Run(() => ReadPageInternal(i, false));
                Array.Copy(bytesRead, 0, memory, i, bytesRead.Length);
                progress?.Report((i + PageSize) * 100 / memory.Length);
            }

            return memory;
        }

        private byte[] ReadPageInternal(int address, bool overdrive)
        {
            if (overdrive) OverdriveSkipRom();
            else SkipRom();

            _adapter.PutByte(CMD_READ_MEMORY);
            _adapter.PutByte((byte)(address & 0xFF));
            _adapter.PutByte((byte)((address >> 8) & 0xFF));

            byte[] response = new byte[PageSize];
            _adapter.GetBlock(response, 0, PageSize);

            return response;
        }

        public byte[] ReadPage(int pageNumber)
        {
            if (pageNumber < 0 || pageNumber > 3)
                throw new ArgumentOutOfRangeException("Page must be 0 to 3");

            ushort startAddress = (ushort)(pageNumber * PageSize);

            SkipRom();
            _adapter.PutByte(CMD_READ_MEMORY);
            _adapter.PutByte((byte)(startAddress & 0xFF));
            _adapter.PutByte((byte)(startAddress >> 8));

            byte[] data = new byte[PageSize];
            for (int i = 0; i < PageSize; i++)
                data[i] = (byte)_adapter.GetByte();

            return data;
        }

        public async Task WriteMemoryAsync(ushort address, byte[] data, bool overdrive = false)
        {
            await Task.Run(() => WriteMemoryInternal(address, data, overdrive));
        }

        public async Task WriteMemoryAsync(ushort address, byte[] data, bool overdrive = false, IProgress<int>? progress = null)
        {
            ushort blockSize = 8;

            for (ushort i = 0; i < data.Length; i += blockSize)
            {
                var block = new byte[Math.Min(blockSize, data.Length - i)];
                Array.Copy(data, i, block, 0, block.Length);
                await Task.Run(() => WriteMemoryRowInternal((ushort)(i + address), block, overdrive));
                progress?.Report((i + block.Length) * 100 / data.Length);
            }
        }

        public void WriteMemory(ushort address, byte[] fullData) =>
            WriteMemoryInternal(address, fullData, false);

        public void WriteMemoryOverdriveSpeed(ushort address, byte[] fullData) =>
            WriteMemoryInternal(address, fullData, true);

        private void WriteMemoryInternal(ushort baseAddress, byte[] fullData, bool overdrive)
        {
            for (int addr = 0; addr < fullData.Length; addr += 8)
            {
                var chunk = fullData.Skip(addr).Take(8).PadRight(8).ToArray();
                Logger.Debug($"Writing 8 bytes to address: {baseAddress + addr} ");

                var stopwatch = Stopwatch.StartNew();
                WriteScratchpad((ushort)(baseAddress + addr), chunk, overdrive);
                stopwatch.Stop();

                Logger.Debug($"Done in {stopwatch.ElapsedMilliseconds} ms");
            }
        }

        private void WriteMemoryRowInternal(ushort baseAddress, byte[] fullData, bool overdrive)
        {
            if (baseAddress % 8 != 0)
            {
                Logger.Error($"Write Skipped: 0x{baseAddress:X4} is not 8-byte aligned.");
                return;
            }

            byte[] chunk = new byte[8];
            for (int i = 0; i < chunk.Length; i++)
                chunk[i] = 0xFF;

            if (fullData != null)
                Array.Copy(fullData, chunk, Math.Min(fullData.Length, 8));

            string hexData = BitConverter.ToString(chunk);
            Logger.Debug($"Writing row: 0x{baseAddress:X2} | Data: [{hexData}]");

            var stopwatch = Stopwatch.StartNew();
            WriteScratchpad(baseAddress, chunk, overdrive);
            stopwatch.Stop();
            Logger.Debug($"Done in {stopwatch.ElapsedMilliseconds} ms");
        }

        private void WriteScratchpad(ushort address, byte[] data, bool overdrive)
        {
            if (data.Length > 8)
                throw new ArgumentException("Must write up to 8 bytes only");

            if (overdrive) OverdriveSkipRom();
            else SkipRom();

            _adapter.PutByte(CMD_WRITE_SCRATCHPAD);
            _adapter.PutByte((byte)(address & 0xFF));
            _adapter.PutByte((byte)(address >> 8));

            foreach (var b in data) _adapter.PutByte(b);

            if (overdrive) OverdriveSkipRom();
            else SkipRom();

            _adapter.PutByte(CMD_READ_SCRATCHPAD);

            byte[] scratchpad = new byte[13];
            for (int i = 0; i < scratchpad.Length; i++)
                scratchpad[i] = (byte)_adapter.GetByte();

            byte authCode = scratchpad[2];

            if (overdrive) OverdriveSkipRom();
            else SkipRom();

            _adapter.PutByte(CMD_COPY_SCRATCHPAD);
            _adapter.PutByte((byte)(address & 0xFF));
            _adapter.PutByte((byte)(address >> 8));
            _adapter.PutByte(authCode);
        }

        #region helpers

        private static string Reverse1WireHexAddress(byte[] buff)
        {
            return string.Join(" ", System.Linq.Enumerable.Reverse(buff)
                .Select(b => b.ToString("X2")));
        }

        #endregion
    }
}
