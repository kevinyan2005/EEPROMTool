using System;
using System.Configuration;
using System.ComponentModel;
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

        public ICommand ReadEepromCommand { get; }
        public ICommand WriteEepromCommand { get; }
        public ICommand EraseEepromCommand { get; }
        public ICommand LoadJsonCommand { get; }
        public ICommand LoadRawTxtCommand { get; }
        public ICommand SaveJsonCommand { get; }
        public ICommand ExportHexCommand { get; }
        public ICommand SaveRawTxtCommand { get; }
        public ICommand ExitCommand { get; }

        private readonly RelayCommand _readEepromCommand;
        private readonly RelayCommand _writeEepromCommand;
        private readonly RelayCommand _eraseEepromCommand;

        private EepromData _eeprom;
        private byte[] _rawEepromBytes;
        private readonly byte _eraseFillByte;

        public WriteMode[] WriteModes { get; } = (WriteMode[])Enum.GetValues(typeof(WriteMode));

        private WriteMode _selectedWriteMode;
        public WriteMode SelectedWriteMode
        {
            get => _selectedWriteMode;
            set
            {
                if (_selectedWriteMode == value) return;
                _selectedWriteMode = value;
                OnPropertyChanged();
            }
        }

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
                _writeEepromCommand?.RaiseCanExecuteChanged();
                _eraseEepromCommand?.RaiseCanExecuteChanged();
            }
        }

        public MainViewModel(IFileDialogService fileDialogService, IEepromDataManager manager)
        {
            AllowEditIdentification = GetAppSettingBool("AllowEditIdentification", defaultValue: true);
            AllowEditCalibration = GetAppSettingBool("AllowEditCalibration", defaultValue: true);
            _showProbeUsageDate = GetAppSettingBool("ShowProbeUsageDate", defaultValue: true);
            _eraseFillByte = GetAppSettingByte("EraseFillByte", 0x00);

            _fileDialogService = fileDialogService;
            _manager = manager;
            Connectivity = new ConnectivityViewModel(_manager, OnPortDisconnected);
            Connectivity.PropertyChanged += OnConnectivityPropertyChanged;

            _eeprom = new EepromData();
            Identification = new IdentificationViewModel(_eeprom.Id);
            Calibration = new CalibrationViewModel(_eeprom.Calibration);
            User = new UserDataViewModel(_eeprom.User);
            User.Schema = GetAppSettingUShort("UserDataVersion", 1);

            _readEepromCommand = new RelayCommand(async () => await LoadMemoryAsync(), CanStartOperation);
            _writeEepromCommand = new RelayCommand(async () => await WriteMemoryAsync(), CanStartOperation);
            _eraseEepromCommand = new RelayCommand(async () => await EraseMemoryAsync(), CanStartOperation);
            ReadEepromCommand = _readEepromCommand;
            WriteEepromCommand = _writeEepromCommand;
            EraseEepromCommand = _eraseEepromCommand;
            LoadJsonCommand = new RelayCommand(LoadJson);
            LoadRawTxtCommand = new RelayCommand(LoadRawTxt);
            SaveJsonCommand = new RelayCommand(SaveJson);
            ExportHexCommand = new RelayCommand(ExportHex);
            SaveRawTxtCommand = new RelayCommand(SaveRawTxt);
            ExitCommand = new RelayCommand(() => Application.Current.Shutdown());

            _selectedWriteMode = WriteMode.UserDataOnly;
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
            try { await ReadEepromInternalAsync(); }
            finally { IsBusy = false; }
        }

        private async Task WriteMemoryAsync()
        {
            var (dialogTitle, message) = SelectedWriteMode switch
            {
                WriteMode.UserDataOnly => ("Confirm Write", "Proceed with writing user data to EEPROM?"),
                WriteMode.Erase        => ("Confirm Erase", "Erase the entire EEPROM?"),
                _                      => ("Confirm Write", "Proceed with writing entire EEPROM?")
            };

            var dialog = new ConfirmWriteDialog
            {
                Owner = Application.Current?.MainWindow,
                DialogTitle = dialogTitle,
                Message = message
            };
            dialog.DataContext = dialog;

            if (dialog.ShowDialog() != true) return;

            IsBusy = true;
            try { await ExecuteWriteAsync(SelectedWriteMode); }
            finally { IsBusy = false; }
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

            IsBusy = true;
            try { await ExecuteWriteAsync(WriteMode.Erase); }
            finally { IsBusy = false; }
        }

        private async Task ExecuteWriteAsync(WriteMode mode)
        {
            var progress = new Progress<int>(p => Progress = p);
            var bytes = await _manager.WriteAsync(_eeprom, mode, _eraseFillByte, progress);
            Progress = 0;
            _rawEepromBytes = (byte[])bytes.Clone();
            HexAsciiText = HexFormatter.FormatHexAscii(bytes);

            if (mode == WriteMode.Erase)
                ClearUiState();
            else
                ParseEeprom(bytes);
        }

        private void LoadJson()
        {
            var path = _fileDialogService.OpenFile("JSON files|*.json");
            if (path == null) return;

            _eeprom = _manager.LoadFromJson(path);
            _rawEepromBytes = _manager.Encode(_eeprom);

            Identification = new IdentificationViewModel(_eeprom.Id, loadFromData: true);
            Calibration = new CalibrationViewModel(_eeprom.Calibration, loadFromData: true);
            User = new UserDataViewModel(_eeprom.User, loadFromData: true);

            OnPropertyChanged(nameof(Identification));
            OnPropertyChanged(nameof(Calibration));
            OnPropertyChanged(nameof(User));
        }

        private void SaveJson()
        {
            var path = _fileDialogService.SaveFile("JSON files|*.json", BuildDefaultFileName());
            if (path == null) return;
            _manager.SaveToJson(_eeprom, path);
        }

        private void LoadRawTxt()
        {
            var path = _fileDialogService.OpenFile("Text files|*.txt");
            if (path == null) return;

            try
            {
                var bytes = _manager.LoadRawHex(path);
                _rawEepromBytes = (byte[])bytes.Clone();
                HexAsciiText = HexFormatter.FormatHexAscii(bytes);
                ParseEeprom(bytes);
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Failed to load raw EEPROM text file.");
                MessageBox.Show(
                    "The selected file does not contain valid EEPROM raw hex data.",
                    "Load RAW txt Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        private void SaveRawTxt()
        {
            var path = _fileDialogService.SaveFile("Text files|*.txt", BuildDefaultFileName());
            if (path == null) return;
            var data = _rawEepromBytes != null && _rawEepromBytes.Length > 0
                ? _rawEepromBytes
                : _manager.Encode(_eeprom);
            _manager.SaveRawHex(data, path);
        }

        private void ExportHex()
        {
            var path = _fileDialogService.SaveFile("Text files|*.txt", "hex_dump_" + BuildDefaultFileName());
            if (path == null) return;
            File.WriteAllText(path, HexAsciiText ?? string.Empty);
        }

        #endregion

        #region Helpers

        private bool CanStartOperation() => Connectivity.IsPortOpen && !IsBusy;

        private string BuildDefaultFileName()
        {
            var serial = Identification?.SerialNumber?.Trim() ?? string.Empty;
            var timestamp = DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");
            return string.IsNullOrEmpty(serial) ? timestamp : $"{serial}_{timestamp}";
        }

        private async Task ReadEepromInternalAsync()
        {
            var progress = new Progress<int>(p => Progress = p);
            var bytes = await _manager.ReadRawAsync(progress);
            Progress = 0;
            _rawEepromBytes = (byte[])bytes.Clone();
            HexAsciiText = HexFormatter.FormatHexAscii(bytes);
            ParseEeprom(bytes);
        }

        private void ParseEeprom(byte[] raw)
        {
            try
            {
                _eeprom = _manager.Decode(raw);

                Identification = new IdentificationViewModel(_eeprom.Id, loadFromData: true);
                Calibration    = new CalibrationViewModel(_eeprom.Calibration, loadFromData: true);
                User           = new UserDataViewModel(_eeprom.User, loadFromData: true);

                OnPropertyChanged(nameof(Identification));
                OnPropertyChanged(nameof(Calibration));
                OnPropertyChanged(nameof(User));
            }
            catch (InvalidDataException ex)
            {
                Logger.Error($"CRC validation failed: {ex.Message}");
                MessageBox.Show(
                    $"{ex.Message}\n\n See raw data in the HEX view.",
                    "CRC Validation Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
            catch (Exception ex)
            {
                Logger.Error($"Failed to parse EEPROM data: {ex.Message}");
                throw;
            }
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
            _writeEepromCommand?.RaiseCanExecuteChanged();
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
