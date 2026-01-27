using Newtonsoft.Json;
using OneWire.Common;
using OneWireController;
using OneWireEEPROMWpfApp.Models;
using slf4net;
using System;
using System.Diagnostics;
using System.IO;
using System.Globalization;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using OneWireEEPROMWpfApp.Views;
using System.Configuration;

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
        public ICommand EraseEepromCommand { get; }
        public ICommand LoadJsonCommand { get; }
        public ICommand SaveJsonCommand { get; }
        public ICommand ExportHexCommand { get; }

        private EepromData _eeprom;

        private DS2431Helper? _helper;

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
        private readonly byte _eraseFillByte;

        private bool _allowEditIdentification;
        public bool AllowEditIdentification
        {
            get => _allowEditIdentification;
            set
            {
                if (_allowEditIdentification == value) return;
                _allowEditIdentification = value;
                OnPropertyChanged();
            }
        }

        private bool _allowEditCalibration;
        public bool AllowEditCalibration
        {
            get => _allowEditCalibration;
            set
            {
                if (_allowEditCalibration == value) return;
                _allowEditCalibration = value;
                OnPropertyChanged();
            }
        }

        private bool _showOverdriveCheckbox;
        public bool ShowOverdriveCheckbox
        {
            get => _showOverdriveCheckbox;
            set
            {
                if (_showOverdriveCheckbox == value) return;
                _showOverdriveCheckbox = value;
                OnPropertyChanged();
            }
        }

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
                        _helper?.EnterOverdrive();
                    }
                    else
                    {
                        _helper?.EnterStandard();
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
            SelectedPort = ConfigurationManager.AppSettings["DefaultPort"] ?? "USB1";

            AllowEditIdentification = GetAppSettingBool("AllowEditIdentification", defaultValue: true);
            AllowEditCalibration = GetAppSettingBool("AllowEditCalibration", defaultValue: true);
            ShowOverdriveCheckbox = GetAppSettingBool("ShowOverdriveCheckbox", defaultValue: false);
            _eraseFillByte = GetAppSettingByte("EraseFillByte", 0x00);
            
            _fileDialogService = fileDialogService;

            _eeprom = new EepromData();

            Identification = new IdentificationViewModel(_eeprom.Id);
            Calibration = new CalibrationViewModel(_eeprom.Calibration);
            User = new UserDataViewModel(_eeprom.User);

            TogglePortCommand = new RelayCommand(TogglePort);
            ReadEepromCommand = new RelayCommand(LoadMemory);
            WriteEepromCommand = new RelayCommand(WriteMemory);
            EraseEepromCommand = new RelayCommand(EraseMemory);
            LoadJsonCommand = new RelayCommand(LoadJson);
            SaveJsonCommand = new RelayCommand(SaveJson);
            ExportHexCommand = new RelayCommand(ExportHex);


            _helper = null;
           
        }

        private static bool GetAppSettingBool(string key, bool defaultValue)
        {
            var raw = ConfigurationManager.AppSettings[key];
            if (bool.TryParse(raw, out var value))
            {
                return value;
            }

            return defaultValue;
        }

        private static byte GetAppSettingByte(string key, byte defaultValue)
        {
            var raw = ConfigurationManager.AppSettings[key];
            if (string.IsNullOrWhiteSpace(raw))
            {
                return defaultValue;
            }

            raw = raw.Trim();
            if (raw.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
            {
                if (byte.TryParse(raw.Substring(2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var hexValue))
                {
                    return hexValue;
                }
            }
            else if (byte.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value))
            {
                return value;
            }

            return defaultValue;
        }

        public void OnAppClosing()
        {
            // Save settings, prompt user, release resources, etc.
            _helper?.Disconnect();
        }


        #region

        private void TogglePort()
        {
            if (IsPortOpen)
            {
                _helper?.Reset();
                _helper?.Disconnect();
                _helper = null;
                IsPortOpen = false;
            }
            else if (!string.IsNullOrEmpty(SelectedPort))
            {
                try
                {
                    _helper = new DS2431Helper(SelectedPort);
                    _helper.Connect();
                    _helper.OWReset();
                    IsPortOpen = true;
                }
                catch (Exception ex)
                {
                    Logger.Error(ex, "Failed to connect to 1-Wire adapter.");
                    MessageBox.Show(
                        "1-Wire adapter not available. Connect the USB adapter and try again.",
                        "Connection Error",
                        MessageBoxButton.OK,
                        MessageBoxImage.Error);
                    _helper?.Disconnect();
                    _helper = null;
                    IsPortOpen = false;
                }
            }
        }

        private void LoadJson()
        {
            Logger.Info("Loading EEPROM data from JSON file");
            var path = _fileDialogService.OpenFile("JSON files|*.json");
            if (path == null) return;

            var json = File.ReadAllText(path);
            _eeprom = JsonConvert.DeserializeObject<EepromData>(json)!;

            // Re-wrap VMs around the new data
            Identification = new IdentificationViewModel(_eeprom.Id, loadFromData: true);
            Calibration = new CalibrationViewModel(_eeprom.Calibration, loadFromData:true);
            User = new UserDataViewModel(_eeprom.User, loadFromData: true);

            // Notify UI that these VM references changed
            OnPropertyChanged(nameof(Identification));
            OnPropertyChanged(nameof(Calibration));
            OnPropertyChanged(nameof(User));
        }

        private void SaveJson()
        {
            Logger.Info("Saving EEPROM data to JSON file");
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

        private void ExportHex()
        {
            var path = _fileDialogService.SaveFile("Text files|*.txt");
            if (path == null) return;

            File.WriteAllText(path, HexAsciiText ?? string.Empty);
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
            var confirmDialog = new ConfirmWriteDialog
            {
                Owner = Application.Current?.MainWindow
            };

            if (confirmDialog.ShowDialog() != true)
            {
                return;
            }

            WriteEepromAsync();
        }

        private void EraseMemory()
        {
            var confirmDialog = new ConfirmEraseDialog
            {
                Owner = Application.Current?.MainWindow
            };

            if (confirmDialog.ShowDialog() != true)
            {
                return;
            }

            EraseEepromAsync();
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

            //Read EEPROM after writing
            LoadMemory();
        }

        private async Task EraseEepromAsync()
        {
            var progressIndicator = new Progress<int>(percent => Progress = percent);
            var eepromImage = new byte[128];
            for (var i = 0; i < eepromImage.Length; i++)
            {
                eepromImage[i] = _eraseFillByte;
            }

            await MeasureAsync(
                () => _helper.WriteMemoryAsync(0, eepromImage, false, progressIndicator),
                "Erase Entire EEPROM memory"
            );

            Logger.Info("Erase operation completed.");
            Progress = 0; // Reset progress
            LoadMemory();
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
				Logger.Debug("Calibration data parsed successfully");

                // User-defined data
                offset = 80; //8-byte alignment
                User.ZeroValue = BitConverter.ToUInt32(eeprom, offset);
                User.EqualizationFactor = BitConverter.ToUInt32(eeprom, offset + 4);
                User.ProbeSerialNumber = Encoding.ASCII.GetString(eeprom, offset + 8, 16).TrimEnd('\0');
                User.ProbeExpiryDate = new DateTime(BitConverter.ToInt64(eeprom, offset + 24));
                User.ProbeUsageDate = new DateTime(BitConverter.ToInt64(eeprom, offset + 32));
                User.Crc = BitConverter.ToUInt16(eeprom, offset + 40);
                Logger.Debug("User-defined data parsed successfully");

            }
            catch (Exception e)
            {
                Logger.Error($"Failed to parse data: {e.Message}");
                throw;
            }
        }

        #endregion
    }
}
