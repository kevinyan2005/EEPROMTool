using Newtonsoft.Json;
using OneWire.Common;
using OneWireController;
using OneWireEEPROMWpfApp.Models;
using slf4net;
using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;

namespace OneWireEEPROMWpfApp.ViewModels
{
    public class MainViewModel : ViewModelBase
    {
        private static ILogger Logger { get; } = LoggerFactory.GetLogger(nameof(MainViewModel));

        private readonly IFileDialogService _fileDialogService;
        public IdentificationViewModel Identification { get; private set; }
        public CalibrationViewModel Calibration { get; private set; }
        public UserDataViewModel User { get; private set; }

        public ICommand TogglePortCommand { get; }
        public ICommand ReadEepromCommand { get; }
        public ICommand WriteEepromCommand { get; }
        public ICommand LoadJsonCommand { get; }
        public ICommand SaveJsonCommand { get; }

        private EepromData _eeprom;

        private readonly DS2431Helper _helper;

        private string _selectedPort;
        public string SelectedPort
        {
            get => _selectedPort;
            set
            {
                _selectedPort = value;
                OnPropertyChanged();
            }
        }

        private bool _isPortOpen;
        public bool IsPortOpen
        {
            get => _isPortOpen;
            set
            {
                _isPortOpen = value;
                OnPropertyChanged();
            }
        }

        private bool _useOverrideSpeed;

        /// <summary>
        /// True = Override speed, False = Standard speed
        /// </summary>
        public bool UseOverrideSpeed
        {
            get => _useOverrideSpeed;
            set
            {
                if (_useOverrideSpeed != value)
                {
                    _useOverrideSpeed = value;
                    OnPropertyChanged();

                    // Call hardware switch when toggled
                    if (_useOverrideSpeed)
                    {
                        _helper.EnterOverdrive();
                    }
                    else
                    {
                        _helper.EnterStandard();
                    }
                }
            }
        }
        private int _progress;
        public int Progress
        {
            get => _progress;
            set
            {
                _progress = value;
                OnPropertyChanged(nameof(Progress));
            }
        }

        private string _hexAsciiText;
        public string HexAsciiText
        {
            get => _hexAsciiText;
            set { _hexAsciiText = value; OnPropertyChanged(); }
        }
        
        public MainViewModel(IFileDialogService fileDialogService)
        {
            SelectedPort = "USB1";
            
            _fileDialogService = fileDialogService;

            _eeprom = new EepromData();

            Identification = new IdentificationViewModel(_eeprom.Id);
            Calibration = new CalibrationViewModel(_eeprom.Calibration);
            User = new UserDataViewModel(_eeprom.User);

            TogglePortCommand = new RelayCommand(TogglePort);
            ReadEepromCommand = new RelayCommand(LoadMemory);
            WriteEepromCommand = new RelayCommand(WriteMemory);
            LoadJsonCommand = new RelayCommand(LoadJson);
            SaveJsonCommand = new RelayCommand(SaveJson);


            try
            {
                _helper = new DS2431Helper(SelectedPort);
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                throw;
            }
           
        }

        public void OnAppClosing()
        {
            // Save settings, prompt user, release resources, etc.
            _helper.Disconnect();
        }


        #region
        private void LoadJson()
        {
            var path = _fileDialogService.OpenFile("JSON files|*.json");
            if (path == null) return;

            var json = File.ReadAllText(path);
            _eeprom = JsonConvert.DeserializeObject<EepromData>(json)!;

            // Re-wrap VMs around the new data
            Identification = new IdentificationViewModel(_eeprom.Id);
            Calibration = new CalibrationViewModel(_eeprom.Calibration);
            User = new UserDataViewModel(_eeprom.User);

            // Notify UI that these VM references changed
            OnPropertyChanged(nameof(Identification));
            OnPropertyChanged(nameof(Calibration));
            OnPropertyChanged(nameof(User));
        }

        private void TogglePort()
        {
            if (IsPortOpen)
            {
                _helper.Reset();
                IsPortOpen = false;
            }
            else if (!string.IsNullOrEmpty(SelectedPort))
            {
                _helper.Connect();
                _helper.OWReset();
                IsPortOpen = true;
            }
        }

        private void SaveJson()
        {
            var path = _fileDialogService.SaveFile("JSON files|*.json");
            if (path == null) return;

            var json = JsonConvert.SerializeObject(
                _eeprom,
                Formatting.Indented, //pretty-print 
                new JsonSerializerSettings
                {
                    DateFormatString = "yyyy-MM-ddTHH:mm:ss"  //ISO 8601
                });
            File.WriteAllText(path, json);
        }
        
        private async Task<T> MeasureAsync<T>(Func<Task<T>> operation, string operationName)
        {
            Logger.Info($"{operationName} started...");
            var stopwatch = Stopwatch.StartNew();

            try
            {
                var result = await operation();
                stopwatch.Stop();
                Logger.Info($"{operationName} completed in {stopwatch.ElapsedMilliseconds:N0} ms.");
                return result;
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                Logger.Error(ex, $"{operationName} failed after {stopwatch.ElapsedMilliseconds:N0} ms.");
                throw;
            }
        }

        private async Task MeasureAsync(Func<Task> operation, string operationName)
        {
            Logger.Info($"{operationName} started...");
            var stopwatch = Stopwatch.StartNew();

            try
            {
                await operation();
                stopwatch.Stop();
                Logger.Info($"{operationName} completed in {stopwatch.ElapsedMilliseconds:N0} ms.");
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                Logger.Error(ex, $"{operationName} failed after {stopwatch.ElapsedMilliseconds:N0} ms.");
                throw;
            }
        }

        private void LoadMemory()
        {
            ReadEepromAsync();
        }

        private async Task ReadEepromAsync()
        {
            //Read the entire memory in one continuous read operation
            var bytes = await MeasureAsync(
                () => _helper.ReadMemoryAsync(0, 128, overdrive: false),
                "Read Entire EEPROM memory");

            // Read 8 bytes or a page at a time
            //var progressIndicator = new Progress<int>(percent => Progress = percent);

            //var bytes = await MeasureAsync(
            //    () => _helper.ReadMemoryAsync(0, 128, overdrive: false, progressIndicator),
            //    "Read Entire EEPROM memory");

            //Progress = 0; // Reset progress

            HexAsciiText = FormatHexAscii(bytes);
            ParseEeprom(bytes);
        }

        //private async Task ReadEepromAsync()
        //{
        //    Logger.Info("Read Entire EEPROM memory...");
        //    var stopwatch = Stopwatch.StartNew();

        //    var bytes = !UseOverrideSpeed ? await _helper.ReadMemoryAsync(0, 128, overdrive: false) 
        //        : await _helper.ReadMemoryAsync(0, 128, overdrive: true);

        //    stopwatch.Stop();
        //    Logger.Info($"ReadEntireMemory took {stopwatch.ElapsedMilliseconds:N0} ms.");
        //    HexAsciiText = FormatHexAscii(bytes);
        //    ParseEeprom(bytes);
        //}

        private void WriteMemory()
        {
            WriteEepromAsync();
        }

        private async Task WriteEepromAsync()
        {
            var progressIndicator = new Progress<int>(percent => Progress = percent);

            byte[] identBytes = _eeprom.Id.ToBytes();
            byte[] calBytes = _eeprom.Calibration.ToBytes();
            byte[] userBytes = _eeprom.User.ToBytes();
            byte[] eepromImage = ByteHelper.ConcatenateWithPadding(identBytes, calBytes, userBytes);

            await MeasureAsync(
                () => _helper.WriteMemoryAsync(0, eepromImage, false, progressIndicator),
                "Write Entire EEPROM memory"
            );

            Logger.Info("Write operation completed.");
            Progress = 0; // Reset progress
        }

        //private async Task WriteEepromAsync()
        //{
        //    Logger.Info("Write Entire EEPROM memory...");

        //    byte[] identBytes = _eeprom.Id.ToBytes();
        //    byte[] calBytes = _eeprom.Calibration.ToBytes();
        //    byte[] userBytes = _eeprom.User.ToBytes();
        //    byte[] eepromImage = ByteHelper.ConcatenateWithPadding(identBytes, calBytes, userBytes);
        
        //    var stopwatch = Stopwatch.StartNew();

        //    if (UseOverrideSpeed)
        //        await _helper.WriteMemoryAsync(0, eepromImage, true);
        //    else
        //        await _helper.WriteMemoryAsync(0, eepromImage, false);

        //    stopwatch.Stop();
        //    Logger.Info($"WriteEntireMemory took {stopwatch.ElapsedMilliseconds:N0} ms.");

        //    //Read EEPROM after writing
        //    LoadMemory();
        //}

        private string FormatHexAscii(byte[] data, int bytesPerLine = 16)
        {
            StringBuilder sb = new StringBuilder();

            for (int i = 0; i < data.Length; i += bytesPerLine)
            {
                sb.Append($"{i:X8}  ");

                for (int j = 0; j < bytesPerLine; j++)
                {
                    if (i + j < data.Length)
                        sb.Append($"{data[i + j]:X2} ");
                    else
                        sb.Append("   ");

                    if (j == 7) sb.Append(" ");
                }

                sb.Append(" ");

                for (int j = 0; j < bytesPerLine; j++)
                {
                    if (i + j < data.Length)
                    {
                        byte b = data[i + j];
                        char c = (b >= 32 && b <= 126) ? (char)b : '.';
                        sb.Append(c);
                    }
                }

                sb.AppendLine();
            }

            return sb.ToString();
        }

        private void ParseEeprom(byte[] eeprom)
        {
            try
            {
                // Identification (example: first 16 bytes = serial number)
                var offset = 0;
                Identification.DataVersion = BitConverter.ToUInt16(eeprom, offset);
                Identification.DataIdent = BitConverter.ToUInt16(eeprom, offset + 2);
                Identification.ChipModel = Encoding.ASCII.GetString(eeprom, offset + 4, 16).TrimEnd('\0');
                Identification.SerialNumber = Encoding.ASCII.GetString(eeprom, offset + 20, 16).TrimEnd('\0');
                Identification.Crc = BitConverter.ToUInt16(eeprom, offset + 36);
                Logger.Debug("Identification data parsed successfully");

                offset = 40; //8-byte alignment
                // Calibration (example: uint32 at 0x28)
                Calibration.GaugeFactors[0].Value = BitConverter.ToUInt32(eeprom, offset);
                Calibration.GaugeFactors[1].Value = BitConverter.ToUInt32(eeprom, offset + 4);
                Calibration.GaugeFactors[2].Value = BitConverter.ToUInt32(eeprom, offset + 8);
                Calibration.GaugeFactors[3].Value = BitConverter.ToUInt32(eeprom, offset + 12);
                Calibration.ReferenceValue = BitConverter.ToUInt32(eeprom, offset + 16);
                Calibration.ManufactureDate = new DateTime(BitConverter.ToInt64(eeprom, offset + 20));
                Calibration.ExpiryDate = new DateTime(BitConverter.ToInt64(eeprom, offset + 28));
                //Calibration.GaugeType = BitConverter.ToUInt16(eeprom, offset + 36);
                Calibration.GaugeType = Encoding.ASCII.GetString(eeprom, offset + 36, 2).TrimEnd('\0');
                Calibration.Crc = BitConverter.ToUInt16(eeprom, offset + 38);

                //for (int i = 0; i < 4; i++)
                //    Calibration.GaugeFactors[i].Value = ReadUint32Le(eeprom, offset + i * 4);

                //Calibration.ReferenceValue = ReadUint32Le(eeprom, offset + 16);
                //Calibration.ManufactureDate = DateTime.FromBinary(ReadInt64Le(eeprom, offset + 20));
                //Calibration.ExpiryDate = DateTime.FromBinary(ReadInt64Le(eeprom, offset + 28));
                //Calibration.GaugeType = Encoding.ASCII.GetString(eeprom, offset + 36, 2).TrimEnd('\0');
                //Calibration.Crc = BitConverter.ToUInt16(eeprom, offset + 38);
                Logger.Debug("Calibration data parsed successfully");

                // User-defined data
                offset = 80; //8-byte alignment
                User.Crc = BitConverter.ToUInt16(eeprom, offset + 40);
                User.ZeroValue = BitConverter.ToUInt32(eeprom, offset);
                User.EqualizationFactor = BitConverter.ToUInt32(eeprom, offset + 4);
                User.ProbeSerialNumber = Encoding.ASCII.GetString(eeprom, offset + 8, 16).TrimEnd('\0');
                User.ManufactureDate = new DateTime(BitConverter.ToInt64(eeprom, offset + 24));
                User.ProbeUsage = new DateTime(BitConverter.ToInt64(eeprom, offset + 32));
                //User.Crc = BitConverter.ToUInt16(eeprom, offset + 40);
                Logger.Debug("User-defined data parsed successfully");

            }
            catch (Exception e)
            {
                Logger.Error($"Failed to parse data: {e.Message}");
                throw;
            }
        }

        private static uint ReadUint32Le(byte[] buffer, int offset)
        {
            byte[] temp = new byte[4];
            Array.Copy(buffer, offset, temp, 0, 4);
            if (!BitConverter.IsLittleEndian) Array.Reverse(temp);
            return BitConverter.ToUInt32(temp, 0);
        }

        private static long ReadInt64Le(byte[] buffer, int offset)
        {
            byte[] temp = new byte[8];
            Array.Copy(buffer, offset, temp, 0, 8);
            if (!BitConverter.IsLittleEndian) Array.Reverse(temp);
            return BitConverter.ToInt64(temp, 0);
        }

        #endregion
    }
}
