using System;
using System.Configuration;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using OneWire.Common;
using OneWire.Core;
using OneWire.UI.Wpf.Views;
using slf4net;

namespace OneWire.UI.Wpf.ViewModels
{
    public class MainViewModel : ViewModelBase
    {
        private static ILogger Logger { get; } = LoggerFactory.GetLogger(nameof(MainViewModel));

        private readonly IFileDialogService _fileDialogService;
        private readonly IEepromDataManager _manager;

        public ConnectivityViewModel Connectivity { get; }
        public IdentificationViewModel Identification { get; private set; }
        public CalibrationViewModel Calibration { get; private set; }
        public UserDataViewModel User { get; private set; }
        public OperationHistoryViewModel History { get; }

        public ICommand ReadEepromCommand { get; }
        public ICommand WriteEntireEepromCommand { get; }
        public ICommand WriteUserDataCommand { get; }
        public ICommand EraseEepromCommand { get; }
        public ICommand LoadJsonCommand { get; }
        public ICommand LoadRawTxtCommand { get; }
        public ICommand SaveJsonCommand { get; }
        public ICommand ExportHexCommand { get; }
        public ICommand SaveRawTxtCommand { get; }
        public ICommand ExitCommand { get; }

        private readonly RelayCommand _readEepromCommand;
        private readonly RelayCommand _writeEntireEepromCommand;
        private readonly RelayCommand _writeUserDataCommand;
        private readonly RelayCommand _eraseEepromCommand;

        private EepromData _eeprom;
        private byte[] _rawEepromBytes;
        private readonly byte _eraseFillByte;
        private readonly ushort _userDataVersion;

        private bool _allowEditIdentification;
        public bool AllowEditIdentification
        {
            get => _allowEditIdentification;
            set { if (_allowEditIdentification == value) return; _allowEditIdentification = value; OnPropertyChanged(); }
        }

        private bool _allowEditCalibration;
        public bool AllowEditCalibration
        {
            get => _allowEditCalibration;
            set { if (_allowEditCalibration == value) return; _allowEditCalibration = value; OnPropertyChanged(); }
        }

        private bool _showProbeUsageDate;
        public bool ShowProbeUsageDate
        {
            get => _showProbeUsageDate;
            set { if (_showProbeUsageDate == value) return; _showProbeUsageDate = value; OnPropertyChanged(); }
        }

        private bool _showWriteEntireEepromButton;
        public bool ShowWriteEntireEepromButton
        {
            get => _showWriteEntireEepromButton;
            set { if (_showWriteEntireEepromButton == value) return; _showWriteEntireEepromButton = value; OnPropertyChanged(); }
        }

        private bool _showFormatEepromButton;
        public bool ShowFormatEepromButton
        {
            get => _showFormatEepromButton;
            set { if (_showFormatEepromButton == value) return; _showFormatEepromButton = value; OnPropertyChanged(); }
        }

        private bool _checkIdentificationCrc = true;
        public bool CheckIdentificationCrc
        {
            get => _checkIdentificationCrc;
            set { if (_checkIdentificationCrc == value) return; _checkIdentificationCrc = value; OnPropertyChanged(); }
        }

        private bool _checkCalibrationCrc = true;
        public bool CheckCalibrationCrc
        {
            get => _checkCalibrationCrc;
            set { if (_checkCalibrationCrc == value) return; _checkCalibrationCrc = value; OnPropertyChanged(); }
        }

        private bool _checkUserCrc = true;
        public bool CheckUserCrc
        {
            get => _checkUserCrc;
            set { if (_checkUserCrc == value) return; _checkUserCrc = value; OnPropertyChanged(); }
        }

        private int _progress;
        public int Progress
        {
            get => _progress;
            set { _progress = value; OnPropertyChanged(); }
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
                _writeEntireEepromCommand?.RaiseCanExecuteChanged();
                _writeUserDataCommand?.RaiseCanExecuteChanged();
                _eraseEepromCommand?.RaiseCanExecuteChanged();
            }
        }

        public MainViewModel(IFileDialogService fileDialogService, IEepromDataManager manager)
        {
            AllowEditIdentification = GetAppSettingBool("AllowEditIdentification", defaultValue: true);
            AllowEditCalibration = GetAppSettingBool("AllowEditCalibration", defaultValue: true);
            _showProbeUsageDate = GetAppSettingBool("ShowProbeUsageDate", defaultValue: true);
            _showWriteEntireEepromButton = GetAppSettingBool("ShowWriteEntireEepromButton", defaultValue: true);
            _showFormatEepromButton = GetAppSettingBool("ShowFormatEepromButton", defaultValue: true);
            _eraseFillByte = GetAppSettingByte("EraseFillByte", 0x00);
            _userDataVersion = GetAppSettingUShort("UserDataVersion", 1);

            _fileDialogService = fileDialogService;
            _manager = manager;
            Connectivity = new ConnectivityViewModel(_manager, OnPortDisconnected);
            Connectivity.PropertyChanged += OnConnectivityPropertyChanged;
            History = new OperationHistoryViewModel();

            _eeprom = new EepromData();
            Identification = new IdentificationViewModel(_eeprom.Id);
            Calibration = new CalibrationViewModel(_eeprom.Calibration);
            User = new UserDataViewModel(_eeprom.User);
            User.Schema = _userDataVersion;

            _readEepromCommand = new RelayCommand(async () => await LoadMemoryAsync(), CanStartOperation);
            _writeEntireEepromCommand = new RelayCommand(async () => await WriteMemoryAsync(WriteMode.Entire), CanStartOperation);
            _writeUserDataCommand = new RelayCommand(async () => await WriteMemoryAsync(WriteMode.UserDataOnly), CanStartOperation);
            _eraseEepromCommand = new RelayCommand(async () => await EraseMemoryAsync(), CanStartOperation);
            ReadEepromCommand = _readEepromCommand;
            WriteEntireEepromCommand = _writeEntireEepromCommand;
            WriteUserDataCommand = _writeUserDataCommand;
            EraseEepromCommand = _eraseEepromCommand;
            LoadJsonCommand = new RelayCommand(LoadJson);
            LoadRawTxtCommand = new RelayCommand(LoadRawTxt);
            SaveJsonCommand = new RelayCommand(SaveJson);
            ExportHexCommand = new RelayCommand(ExportHex);
            SaveRawTxtCommand = new RelayCommand(SaveRawTxt);
            ExitCommand = new RelayCommand(() => Application.Current.Shutdown());

            _rawEepromBytes = Array.Empty<byte>();
        }

        public void OnAppClosing()
        {
            Connectivity.Close();
        }

        #region Commands

        private async Task LoadMemoryAsync()
        {
            IsBusy = true;
            var stopwatch = Stopwatch.StartNew();
            var result = "Success";
            try
            {
                if (!await ReadEepromInternalAsync())
                    result = "Success (CRC check failed)";
            }
            catch (Exception ex)
            {
                result = $"Failed: {ex.Message}";
                Logger.Error(ex, "Read EEPROM failed.");
                throw;
            }
            finally
            {
                stopwatch.Stop();
                History.Add("Read EEPROM", BuildDeviceLabel(), result, stopwatch.Elapsed);
                IsBusy = false;
            }
        }

        private async Task WriteMemoryAsync(WriteMode mode)
        {
            User.Schema = _userDataVersion;

            var (dialogTitle, message) = mode == WriteMode.UserDataOnly
                ? ("Confirm Write", "Proceed with writing user data to EEPROM?")
                : ("Confirm Write", "Proceed with writing entire EEPROM?");

            var dialog = new ConfirmWriteDialog
            {
                Owner = Application.Current?.MainWindow,
                DialogTitle = dialogTitle,
                Message = message
            };
            dialog.DataContext = dialog;

            if (dialog.ShowDialog() != true) return;

            var operationName = mode == WriteMode.UserDataOnly ? "Write User Data" : "Write Entire EEPROM";
            await ProgramNextSequenceAsync(mode, operationName);
        }

        /// <summary>
        /// Writes the current EEPROM data, then repeatedly offers to program the next unit in
        /// sequence (incrementing the serial number) for production-line style batch programming.
        /// </summary>
        private async Task ProgramNextSequenceAsync(WriteMode mode, string operationName)
        {
            while (true)
            {
                var success = await WriteMemoryTrackedAsync(mode, operationName);
                if (!success) return;

                var promptDialog = new ProgramNextEepromDialog
                {
                    Owner = Application.Current?.MainWindow
                };

                if (promptDialog.ShowDialog() != true) return;

                if (!TryAdvanceToNextSerialNumber())
                {
                    MessageBox.Show(
                        "Could not advance to the next serial number automatically (the sequence may be invalid or at its maximum value). Update it manually before writing again.",
                        "Program Next EEPROM",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                    return;
                }

                if (!CanStartOperation())
                {
                    MessageBox.Show(
                        "The device is no longer connected. Reconnect and write manually to continue programming.",
                        "Program Next EEPROM",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                    return;
                }
            }
        }

        /// <summary>
        /// Increments the last (sequence) segment of the probe serial number. Setting
        /// <see cref="UserDataViewModel.Sequence"/> updates the in-memory EEPROM image, refreshes
        /// all dependent serial-number UI fields, and recalculates/redisplays the User Data CRC.
        /// </summary>
        private bool TryAdvanceToNextSerialNumber()
        {
            var currentSequence = User.Sequence;
            if (!int.TryParse(currentSequence, out var sequenceNumber))
                return false;

            var nextSequence = (sequenceNumber + 1).ToString("00", CultureInfo.InvariantCulture);
            if (nextSequence.Length != currentSequence.Length)
                return false; // overflowed the fixed-width sequence segment

            User.Sequence = nextSequence;

            // Validate the updated User Data before allowing the next write to proceed.
            return User["Sequence"] == null && User["LotNumber"] == null;
        }

        private async Task EraseMemoryAsync()
        {
            var dialog = new ConfirmWriteDialog
            {
                Owner = Application.Current?.MainWindow,
                DialogTitle = "Confirm Erase",
                Message = "Erase the entire EEPROM?"
            };
            dialog.DataContext = dialog;

            if (dialog.ShowDialog() != true) return;

            await WriteMemoryTrackedAsync(WriteMode.Erase, "Format EEPROM");
        }

        private async Task<bool> WriteMemoryTrackedAsync(WriteMode mode, string operationName)
        {
            IsBusy = true;
            var stopwatch = Stopwatch.StartNew();
            var result = "Success";
            var success = true;
            try
            {
                if (!await ExecuteWriteAsync(mode))
                    result = "Success (CRC check failed)";
            }
            catch (Exception ex)
            {
                result = $"Failed: {ex.Message}";
                success = false;
                Logger.Error(ex, $"{operationName} failed.");
                MessageBox.Show(
                    $"{operationName} failed: {ex.Message}",
                    "Write Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
            finally
            {
                stopwatch.Stop();
                History.Add(operationName, BuildDeviceLabel(), result, stopwatch.Elapsed);
                IsBusy = false;
            }
            return success;
        }

        private async Task<bool> ExecuteWriteAsync(WriteMode mode)
        {
            var progress = new Progress<int>(p => Progress = p);
            var bytes = await _manager.WriteAsync(_eeprom, mode, _eraseFillByte, progress);
            Progress = 0;
            _rawEepromBytes = (byte[])bytes.Clone();
            HexAsciiText = HexFormatter.FormatHexAscii(bytes);

            if (mode == WriteMode.Erase)
            {
                ClearUiState();
                return true;
            }

            return ParseEeprom(bytes);
        }

        private void LoadJson()
        {
            var path = _fileDialogService.OpenFile("JSON files|*.json", InputFilesDirectory);
            if (path == null) return;

            var stopwatch = Stopwatch.StartNew();
            var result = "Success";
            try
            {
                _eeprom = _manager.LoadFromJson(path);
                _rawEepromBytes = _manager.Encode(_eeprom);

                Identification = new IdentificationViewModel(_eeprom.Id, loadFromData: true);
                Calibration = new CalibrationViewModel(_eeprom.Calibration, loadFromData: true);
                User = new UserDataViewModel(_eeprom.User, loadFromData: true);

                OnPropertyChanged(nameof(Identification));
                OnPropertyChanged(nameof(Calibration));
                OnPropertyChanged(nameof(User));
            }
            catch (Exception ex)
            {
                result = $"Failed: {ex.Message}";
                Logger.Error(ex, "Failed to load EEPROM JSON file.");
                throw;
            }
            finally
            {
                stopwatch.Stop();
                History.Add("Load Image (JSON)", BuildDeviceLabel(), result, stopwatch.Elapsed);
            }
        }

        private void SaveJson()
        {
            var path = _fileDialogService.SaveFile("JSON files|*.json", BuildDefaultFileName(), OutputFilesDirectory);
            if (path == null) return;

            var stopwatch = Stopwatch.StartNew();
            var result = "Success";
            try
            {
                _manager.SaveToJson(_eeprom, path);
            }
            catch (Exception ex)
            {
                result = $"Failed: {ex.Message}";
                Logger.Error(ex, "Failed to save EEPROM JSON file.");
                throw;
            }
            finally
            {
                stopwatch.Stop();
                History.Add("Save Image (JSON)", BuildDeviceLabel(), result, stopwatch.Elapsed);
            }
        }

        private void LoadRawTxt()
        {
            var path = _fileDialogService.OpenFile("Text files|*.txt", InputFilesDirectory);
            if (path == null) return;

            var stopwatch = Stopwatch.StartNew();
            var result = "Success";
            try
            {
                var bytes = _manager.LoadRawHex(path);
                _rawEepromBytes = (byte[])bytes.Clone();
                HexAsciiText = HexFormatter.FormatHexAscii(bytes);
                if (!ParseEeprom(bytes))
                    result = "Success (CRC check failed)";
            }
            catch (Exception ex)
            {
                result = $"Failed: {ex.Message}";
                Logger.Error(ex, "Failed to load raw EEPROM text file.");
                MessageBox.Show(
                    "The selected file does not contain valid EEPROM raw hex data.",
                    "Load RAW txt Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
            finally
            {
                stopwatch.Stop();
                History.Add("Load Image (RAW)", BuildDeviceLabel(), result, stopwatch.Elapsed);
            }
        }

        private void SaveRawTxt()
        {
            var path = _fileDialogService.SaveFile("Text files|*.txt", BuildDefaultFileName(), OutputFilesDirectory);
            if (path == null) return;

            var stopwatch = Stopwatch.StartNew();
            var result = "Success";
            try
            {
                var data = _rawEepromBytes != null && _rawEepromBytes.Length > 0
                    ? _rawEepromBytes
                    : _manager.Encode(_eeprom);
                _manager.SaveRawHex(data, path);
            }
            catch (Exception ex)
            {
                result = $"Failed: {ex.Message}";
                Logger.Error(ex, "Failed to save raw EEPROM text file.");
                throw;
            }
            finally
            {
                stopwatch.Stop();
                History.Add("Save Image (RAW)", BuildDeviceLabel(), result, stopwatch.Elapsed);
            }
        }

        private void ExportHex()
        {
            var path = _fileDialogService.SaveFile("Text files|*.txt", "hex_dump_" + BuildDefaultFileName(), OutputFilesDirectory);
            if (path == null) return;
            File.WriteAllText(path, HexAsciiText ?? string.Empty);
        }

        #endregion

        #region Helpers

        private bool CanStartOperation() => Connectivity.IsPortOpen && !IsBusy;

        private static string GetAppSubDirectory(string name)
        {
            var dir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, name);
            Directory.CreateDirectory(dir);
            return dir;
        }

        private static string InputFilesDirectory => GetAppSubDirectory("InputFiles");

        private static string OutputFilesDirectory => GetAppSubDirectory("OutputFiles");

        private string BuildDeviceLabel()
        {
            var sn = Identification?.SerialNumber?.Trim();
            var probeSn = User?.ProbeSerialNumber?.Trim();

            if (string.IsNullOrEmpty(sn) && string.IsNullOrEmpty(probeSn))
                return "-";

            var label = string.IsNullOrEmpty(sn) ? "-" : sn;
            if (!string.IsNullOrEmpty(probeSn))
                label += $" / Probe: {probeSn}";

            return label;
        }

        private string BuildDefaultFileName()
        {
            var serial = Identification?.SerialNumber?.Trim() ?? string.Empty;
            var timestamp = DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");
            return string.IsNullOrEmpty(serial) ? timestamp : $"{serial}_{timestamp}";
        }

        private async Task<bool> ReadEepromInternalAsync()
        {
            var progress = new Progress<int>(p => Progress = p);
            var bytes = await _manager.ReadRawAsync(progress);
            Progress = 0;
            _rawEepromBytes = (byte[])bytes.Clone();
            HexAsciiText = HexFormatter.FormatHexAscii(bytes);
            return ParseEeprom(bytes);
        }

        private CrcCheckOptions BuildCrcCheckOptions() => new CrcCheckOptions
        {
            CheckIdentification = CheckIdentificationCrc,
            CheckCalibration = CheckCalibrationCrc,
            CheckUser = CheckUserCrc
        };

        private bool ParseEeprom(byte[] raw)
        {
            try
            {
                _eeprom = _manager.Decode(raw, BuildCrcCheckOptions());
                ApplyEepromToViewModels();
                return true;
            }
            catch (CrcValidationException ex)
            {
                // Fields are still fully parsed even though CRC validation failed for one or more
                // sections, so keep showing the parsed data instead of discarding it.
                _eeprom = ex.EepromData;
                ApplyEepromToViewModels();

                Logger.Error($"CRC validation failed: {ex.Message}");
                MessageBox.Show(
                    $"{ex.Message}\n\n Data has still been loaded; verify the affected section(s) before use.",
                    "CRC Validation Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
                return false;
            }
            catch (Exception ex)
            {
                Logger.Error($"Failed to parse EEPROM data: {ex.Message}");
                throw;
            }
        }

        private void ApplyEepromToViewModels()
        {
            Identification = new IdentificationViewModel(_eeprom.Id, loadFromData: true);
            Calibration    = new CalibrationViewModel(_eeprom.Calibration, loadFromData: true);
            User           = new UserDataViewModel(_eeprom.User, loadFromData: true);

            OnPropertyChanged(nameof(Identification));
            OnPropertyChanged(nameof(Calibration));
            OnPropertyChanged(nameof(User));
        }

        private void ClearUiState()
        {
            _eeprom = new EepromData();
            _rawEepromBytes = Array.Empty<byte>();
            Identification = new IdentificationViewModel(_eeprom.Id);
            Calibration = new CalibrationViewModel(_eeprom.Calibration);
            User = new UserDataViewModel(_eeprom.User);
            Progress = 0;
            IsBusy = false;
            OnPropertyChanged(nameof(Identification));
            OnPropertyChanged(nameof(Calibration));
            OnPropertyChanged(nameof(User));
        }

        private void ClearRawDataView() => HexAsciiText = string.Empty;

        private void OnPortDisconnected()
        {
            ClearUiState();
            ClearRawDataView();
        }

        private void OnConnectivityPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName != nameof(ConnectivityViewModel.IsPortOpen))
                return;

            _readEepromCommand?.RaiseCanExecuteChanged();
            _writeEntireEepromCommand?.RaiseCanExecuteChanged();
            _writeUserDataCommand?.RaiseCanExecuteChanged();
            _eraseEepromCommand?.RaiseCanExecuteChanged();
        }

        private static bool GetAppSettingBool(string key, bool defaultValue)
        {
            var raw = ConfigurationManager.AppSettings[key];
            return bool.TryParse(raw, out var v) ? v : defaultValue;
        }

        private static byte GetAppSettingByte(string key, byte defaultValue)
        {
            var raw = ConfigurationManager.AppSettings[key];
            if (string.IsNullOrWhiteSpace(raw)) return defaultValue;
            raw = raw.Trim();
            if (raw.StartsWith("0x", StringComparison.OrdinalIgnoreCase) &&
                byte.TryParse(raw.Substring(2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var hex))
                return hex;
            return byte.TryParse(raw, out var v) ? v : defaultValue;
        }

        private static ushort GetAppSettingUShort(string key, ushort defaultValue)
        {
            var raw = ConfigurationManager.AppSettings[key];
            if (string.IsNullOrWhiteSpace(raw)) return defaultValue;
            raw = raw.Trim();
            if (raw.StartsWith("0x", StringComparison.OrdinalIgnoreCase) &&
                ushort.TryParse(raw.Substring(2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var hex))
                return hex;
            return ushort.TryParse(raw, out var v) ? v : defaultValue;
        }

        #endregion
    }
}
