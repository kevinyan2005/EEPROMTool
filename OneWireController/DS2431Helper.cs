using System;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using DalSemi.OneWire;
using DalSemi.OneWire.Adapter;
using slf4net;

namespace OneWireController
{
    public class DS2431Helper
    {
        private static ILogger Logger { get; } = LoggerFactory.GetLogger(nameof(DS2431Helper));

        private readonly PortAdapter _adapter;

        // DS2431 Command Constants
        private const byte CMD_SKIP_ROM = 0xCC;
        private const byte CMD_OVERDRIVE_SKIP_ROM = 0x3C;
        private const byte CMD_READ_MEMORY = 0xF0;
        private const byte CMD_WRITE_SCRATCHPAD = 0x0F;
        private const byte CMD_READ_SCRATCHPAD = 0xAA;
        private const byte CMD_COPY_SCRATCHPAD = 0x55;

        public byte[] Rom { get; }

        public DS2431Helper(string port)
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
            // get exclusive use of resource
            _adapter.BeginExclusive(true);

            _adapter.SetSearchAllDevices();
            _adapter.TargetAllFamilies();
            _adapter.Speed = OWSpeed.SPEED_REGULAR; //Start at a standard speed

            // get 1-Wire Addresses
            byte[] address = new byte[8];
            // get the first 1-Wire device's address
            // keep in mind the first device is not necessarily the first 
            // device physically located on the network.
            if (_adapter.GetFirstDevice(address, 0))
            {
                do  // get subsequent 1-Wire device addresses
                {
                    Logger.Info($"1-Wire Device Address: {Reverse1WireHexAddress(address)}");
                }
                while (_adapter.GetNextDevice(address, 0));
            }
            // end exclusive use of resource
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
            _adapter.PutByte(CMD_SKIP_ROM); // Skip ROM cmd
        }

        public void OverdriveSkipRom()
        {
            if (!OWReset()) return;
            _adapter.PutByte(CMD_OVERDRIVE_SKIP_ROM); // Override Skip ROM cmd
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
            _adapter.PutByte(CMD_OVERDRIVE_SKIP_ROM); // Overdrive Skip ROM
            _adapter.Speed = OWSpeed.SPEED_OVERDRIVE;
        }

        public void EnterStandard()
        {
            _adapter.Speed = OWSpeed.SPEED_REGULAR;
            _adapter.Reset();
            _adapter.PutByte(CMD_SKIP_ROM); // Skip ROM standard speed
        }

        public Task<byte[]> ReadMemoryAsync(int address, int length, bool overdrive = false) =>
            Task.Run(() => ReadMemoryInternal(address, length, overdrive));

        public async Task<byte[]> ReadMemoryAsync(int address, int length, bool overdrive = false, IProgress<int>? progress = null)
        {
            byte[] memory = new byte[length];
            int blockSize = 8;

            for (int i = 0; i < memory.Length; i += blockSize)
            {
                // Reading a row (8 bytes)
                Logger.Debug($"Reading 8 bytes from address: {i} ");
                var bytesRead = await Task.Run(() => ReadMemoryInternal(i, blockSize, false));
                Array.Copy(bytesRead, 0, memory, i, bytesRead.Length);

                // Report progress
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
        
        public byte[] ReadPage(int pageNumber)
        {
            if (pageNumber < 0 || pageNumber > 3)
                throw new ArgumentOutOfRangeException("Page must be 0 to 3");

            ushort startAddress = (ushort)(pageNumber * 32);

            SkipRom();
            _adapter.PutByte(CMD_READ_MEMORY);  // Read Memory command
            _adapter.PutByte((byte)(startAddress & 0xFF));  // TA1
            _adapter.PutByte((byte)(startAddress >> 8));    // TA2,

            byte[] data = new byte[32];
            for (int i = 0; i < 32; i++)
                data[i] = (byte)_adapter.GetByte(); // read each byte

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

                // Writing a row (8 bytes)
                await Task.Run(() => WriteMemoryRowInternal(i, block, overdrive));

                // Report progress
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
            // 1. Create a fixed-size buffer of 8 bytes
            byte[] chunk = new byte[8];

            if (fullData != null)
            {
                // 2. Copy the data into the buffer. 
                // Math.Min ensures we don't crash if the input is LARGER than 8.
                Array.Copy(fullData, chunk, Math.Min(fullData.Length, 8));
            }

            Logger.Debug($"Writing 8 bytes to address: {baseAddress} ");

            var stopwatch = Stopwatch.StartNew();
            WriteScratchpad((ushort)(baseAddress), chunk, overdrive);
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

            //if (overdrive) Thread.Sleep(15);
            //await Task.Delay(15);
        }

        #region helpers

        private static string Print1WireHexAddress(byte[] buff)
        {
            StringBuilder sb = new StringBuilder(buff.Length * 3);
            for (int i = 7; i > -1; i--)
            {
                sb.Append(buff[i].ToString("X2"));
            }
            return sb.ToString();

        }

        private static string Reverse1WireHexAddress(byte[] buff)
        {
            var reversed = string.Join(" ", buff.Reverse()
                .Select(b => b.ToString("X2")));

            return reversed;
        }

        #endregion

    }
}
