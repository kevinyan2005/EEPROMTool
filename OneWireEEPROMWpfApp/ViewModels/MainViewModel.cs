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
        
        private readonly RelayCommand _readEepromCommand;
        private readonly RelayCommand _writeEepromCommand;
        private readonly RelayCommand _eraseEepromCommand;

        private EepromData _eeprom;

        private DS2431Helper? _helper;

        private const string WriteModeEntire = "Entire EEPROM";
        private const string WriteModeUserData = "User Data Only";
        private const string WriteModeErase = "Erase EEPROM";

        public string[] WriteModes { get; } = { WriteModeEntire, WriteModeUserData, WriteModeErase };

        private string _selectedWriteMode;
        public string SelectedWriteMode
        {
            get => _selectedWriteMode;
            set
            {
                if (_selectedWriteMode == value) return;
                _selectedWriteMode = value;
                OnPropertyChanged();
            }
        }

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
                _readEepromCommand?.RaiseCanExecuteChanged();
                _writeEepromCommand?.RaiseCanExecuteChanged();
                _eraseEepromCommand?.RaiseCanExecuteChanged();
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

        private bool _isBusy;
        public bool IsBusy
        {
            get => _isBusy;
            set
            {
                if (_isBusy == value) return;
                _isBusy = value;
                OnPropertyChanged();
                _readEepromCommand?.RaiseCanExecuteChanged();
                _writeEepromCommand?.RaiseCanExecuteChanged();
                _eraseEepromCommand?.RaiseCanExecuteChanged();
            }
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
            _readEepromCommand = new RelayCommand(async () => await LoadMemoryAsync(), CanStartOperation);
            _writeEepromCommand = new RelayCommand(async () => await WriteMemoryAsync(), CanStartOperation);
            _eraseEepromCommand = new RelayCommand(async () => await EraseMemoryAsync(), CanStartOperation);
            ReadEepromCommand = _readEepromCommand;
            WriteEepromCommand = _writeEepromCommand;
            EraseEepromCommand = _eraseEepromCommand;
            LoadJsonCommand = new RelayCommand(LoadJson);
            SaveJsonCommand = new RelayCommand(SaveJson);
            ExportHexCommand = new RelayCommand(ExportHex);

            _helper = null;
            _selectedWriteMode = WriteModeUserData;
        }


        public void OnAppClosing()
        {
            // Save settings, prompt user, release resources, etc.
            _helper?.Disconnect();
        }


        #region Helpers

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

        private void TogglePort()
        {
            if (IsPortOpen)
            {
                _helper?.Reset();
                _helper?.Disconnect();
                _helper = null;
                IsPortOpen = false;
                ClearUiState();
                ClearRawDataView();
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

        private void ClearUiState()
        {
            _eeprom = new EepromData();

            Identification = new IdentificationViewModel(_eeprom.Id);
            Calibration = new CalibrationViewModel(_eeprom.Calibration);
            User = new UserDataViewModel(_eeprom.User);

            //HexAsciiText = string.Empty;
            Progress = 0;
            IsBusy = false;

            OnPropertyChanged(nameof(Identification));
            OnPropertyChanged(nameof(Calibration));
            OnPropertyChanged(nameof(User));
        }

        private void ClearRawDataView()
        {
            HexAsciiText = string.Empty;
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
            Calibration = new CalibrationViewModel(_eeprom.Calibration, loadFromData: true);
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

        private bool CanStartOperation()
        {
            return IsPortOpen && !IsBusy;
        }

        private async Task LoadMemoryAsync()
        {
            IsBusy = true;
            try
            {
                await ReadEepromAsync();
            }
            finally
            {
                IsBusy = false;
            }
        }

        private async Task ReadEepromAsync(bool parseEeprom = true)
        {
            //Read the entire memory in one continuous read operation
            //var bytes = await MeasureAsync(
            //    () => _helper.ReadMemoryAsync(0, 128, overdrive: false),
            //    "Read Entire EEPROM memory");

            // Read 8 bytes or a page at a time
            //var progressIndicator = new Progress<int>(percent => Progress = percent);

            //var bytes = await MeasureAsync(
            //    () => _helper.ReadMemoryAsync(0, 128, overdrive: false, progressIndicator),
            //    "Read Entire EEPROM memory");

            //Progress = 0; // Reset progress

            var progressIndicator = new Progress<int>(percent => Progress = percent);
            var bytes = await MeasureAsync(
                () => _helper.ReadEntireMemoryAsync(overdrive: false, progressIndicator),
                "Read Entire EEPROM memory");
            Progress = 0; // Reset progress

            HexAsciiText = FormatHexAscii(bytes);
            if (parseEeprom)
            {
                ParseEeprom(bytes);
            }
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

        private async Task WriteMemoryAsync()
        {
            var dialogTitle = "Confirm Write";
            var message = "Proceed with writing entire EEPROM?";

            if (SelectedWriteMode == WriteModeUserData)
            {
                message = "Proceed with writing user data to EEPROM?";
            }
            else if (SelectedWriteMode == WriteModeErase)
            {
                dialogTitle = "Confirm Erase";
                message = "Erase the entire EEPROM?";
            }

            var confirmDialog = new ConfirmWriteDialog
            {
                Owner = Application.Current?.MainWindow,
                DialogTitle = dialogTitle,
                Message = message
            };
            confirmDialog.DataContext = confirmDialog;

            if (confirmDialog.ShowDialog() != true)
            {
                return;
            }

            IsBusy = true;
            try
            {
                if (SelectedWriteMode == WriteModeErase)
                {
                    await EraseEepromAsync();
                }
                else if (SelectedWriteMode == WriteModeUserData)
                {
                    await WriteUserDataEepromAsync();
                }
                else
                {
                    await WriteEntireEepromAsync();
                }
            }
            finally
            {
                IsBusy = false;
            }
        }

        private async Task EraseMemoryAsync()
        {
            // Keeping the UI dialog logic separate
            var confirmDialog = new ConfirmWriteDialog
            {
                Owner = Application.Current?.MainWindow,
                DialogTitle = "Confirm Erase",
                Message = "Erase the entire EEPROM?"
            };
            confirmDialog.DataContext = confirmDialog;

            if (confirmDialog.ShowDialog() == true)
            {
                IsBusy = true;
                try
                {
                    await EraseEepromAsync();
                }
                finally
                {
                    IsBusy = false;
                }
            }
        }

        private async Task EraseEepromAsync()
        {
            var progressIndicator = new Progress<int>(percent => Progress = percent);
            var eepromImage = new byte[128];
            for (var i = 0; i < eepromImage.Length; i++)
            {
                eepromImage[i] = _eraseFillByte;
            }

            await ExecuteEepromWriteAsync(
                0,
                eepromImage,
                "Erase Entire EEPROM memory",
                parseAfterRead: false);

            ClearUiState();
        }

        private async Task WriteEntireEepromAsync()
        {
            byte[] vendorEepromImage = ByteHelper.Concatenate(
                _eeprom.Id.ToBytes(),
                _eeprom.Calibration.ToBytes());

            byte[] eepromImage = ByteHelper.ConcatenateWithPadding(
                vendorEepromImage, 
                _eeprom.User.ToBytes());

            await ExecuteEepromWriteAsync(0, eepromImage, "Write Entire EEPROM memory");
        }

        private async Task WriteUserDataEepromAsync()
        {
            const ushort userAreaStartAddress = 80;
            byte[] userData = _eeprom.User.ToBytes();

            await ExecuteEepromWriteAsync(userAreaStartAddress, userData, "Write User-defined EEPROM memory");
        }

        private async Task ExecuteEepromWriteAsync(
            ushort address,
            byte[] data,
            string description,
            bool parseAfterRead = true)
        {
            if (data == null || data.Length == 0)
            {
                Logger.Warn($"Write cancelled: No data provided for {description}.");
                return;
            }

            var progressIndicator = new Progress<int>(percent => Progress = percent);

            try
            {
                await MeasureAsync(
                    () => _helper.WriteMemoryAsync(address, data, false, progressIndicator),
                    description
                );
                Logger.Info($"{description} completed successfully.");
            }
            finally
            {
                Progress = 0; // Always reset progress even if write fails
                await ReadEepromAsync(parseAfterRead); // Refresh local cache from hardware
               
            }
        }

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
                Identification.DataVersion = ByteHelper.ReadUInt16FromBytesBigEndian(eeprom, offset);
                Identification.DataIdent = Encoding.ASCII.GetString(eeprom, offset + 2, 2).TrimEnd('\0');
                Identification.ChipModel = Encoding.ASCII.GetString(eeprom, offset + 4, 16).TrimEnd('\0');
                Identification.SerialNumber = Encoding.ASCII.GetString(eeprom, offset + 20, 16).TrimEnd('\0');
                Identification.Crc = ByteHelper.ReadUInt16FromBytesBigEndian(eeprom, offset + 36);
                Logger.Debug("Identification data parsed successfully");

                // Calibration (example: uint32 at 0x28)
                offset = 38; 
                Calibration.GaugeFactors[0].Value = ByteHelper.ReadUInt32FromBytesOrNullWithWordSwap(eeprom, offset);
                Calibration.GaugeFactors[1].Value = ByteHelper.ReadUInt32FromBytesOrNullWithWordSwap(eeprom, offset + 4);
                Calibration.GaugeFactors[2].Value = ByteHelper.ReadUInt32FromBytesOrNullWithWordSwap(eeprom, offset + 8);
                Calibration.GaugeFactors[3].Value = ByteHelper.ReadUInt32FromBytesOrNullWithWordSwap(eeprom, offset + 12);
                Calibration.ReferenceValue = ByteHelper.ReadUInt32FromBytesOrNullWithWordSwap(eeprom, offset + 16);
                Calibration.ManufactureDate = ByteHelper.ReadVendorDateTimeOrNull(eeprom, offset + 20);
                Calibration.ExpiryDate = ByteHelper.ReadVendorDateTimeOrNull(eeprom, offset + 28);
                Calibration.GaugeType = Encoding.ASCII.GetString(eeprom, offset + 36, 2).TrimEnd('\0');
                Calibration.Crc = ByteHelper.ReadUInt16FromBytesBigEndian(eeprom, offset + 38);
                Logger.Debug("Calibration data parsed successfully");

                // User-defined data: 36 bytes
                offset = 80; //8-byte alignment
                User.Schema = BitConverter.ToUInt16(eeprom, offset);
                User.ProbeSerialNumber = Encoding.ASCII.GetString(eeprom, offset + 2, 16).TrimEnd('\0');
                User.ProbeExpiryDate = ByteHelper.ReadDateTime(eeprom, offset + 18);
                User.Crc = BitConverter.ToUInt16(eeprom, offset + 26);
                User.ProbeUsageDate = ByteHelper.ReadDateTime(eeprom, offset + 28);
                Logger.Debug("User-defined data parsed successfully");

            }
            catch (Exception e)
            {
                Logger.Error($"Failed to parse data: {e.Message}");
                throw;
            }
        }

        private static void Dump(byte[] data, int bytesPerLine = 8)
        {
            for (int i = 0; i < data.Length; i += bytesPerLine)
            {
                // Format address offset
                var line = $"{i:X4}: ";
                // Append up to bytesPerLine hex bytes
                for (int j = 0; j < bytesPerLine && i + j < data.Length; j++)
                {
                    line += $"{data[i + j]:X2} ";
                }
                // Log the line at Info level (adjust level if needed)
                Logger.Info(line);
            }
        }


        #endregion
    }
}
