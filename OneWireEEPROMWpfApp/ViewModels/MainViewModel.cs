using System;
using System.Configuration;
using System.Globalization;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using OneWire.Common;
using OneWire.Services;
using OneWireEEPROMWpfApp.Views;
using slf4net;

namespace OneWireEEPROMWpfApp.ViewModels
{
    public class MainViewModel : ViewModelBase
    {
        private static ILogger Logger { get; } = LoggerFactory.GetLogger(nameof(MainViewModel));

        private readonly IFileDialogService _fileDialogService;
        private readonly IEepromService _eepromService;
        private readonly IEepromFileService _fileService;
        private readonly IEepromSerializer _eepromSerializer;

        public IdentificationViewModel Identification { get; private set; }
        public CalibrationViewModel Calibration { get; private set; }
        public UserDataViewModel User { get; private set; }

        public ICommand TogglePortCommand { get; }
        public ICommand ReadEepromCommand { get; }
        public ICommand WriteEepromCommand { get; }
        public ICommand EraseEepromCommand { get; }
        public ICommand LoadJsonCommand { get; }
        public ICommand LoadRawTxtCommand { get; }
        public ICommand SaveJsonCommand { get; }
        public ICommand ExportHexCommand { get; }
        public ICommand SaveRawTxtCommand { get; }

        private readonly RelayCommand _readEepromCommand;
        private readonly RelayCommand _writeEepromCommand;
        private readonly RelayCommand _eraseEepromCommand;

        private EepromData _eeprom;
        private byte[] _rawEepromBytes;
        private readonly byte _eraseFillByte;

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
            set { _selectedPort = value; OnPropertyChanged(); }
        }

        public AdapterType[] AdapterTypes { get; } = (AdapterType[])Enum.GetValues(typeof(AdapterType));

        private AdapterType _selectedAdapterType;
        public AdapterType SelectedAdapterType
        {
            get => _selectedAdapterType;
            set
            {
                if (_selectedAdapterType == value) return;
                _selectedAdapterType = value;
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

        private bool _showOverdriveCheckbox;
        public bool ShowOverdriveCheckbox
        {
            get => _showOverdriveCheckbox;
            set { if (_showOverdriveCheckbox == value) return; _showOverdriveCheckbox = value; OnPropertyChanged(); }
        }

        private bool _showProbeUsageDate;
        public bool ShowProbeUsageDate
        {
            get => _showProbeUsageDate;
            set { if (_showProbeUsageDate == value) return; _showProbeUsageDate = value; OnPropertyChanged(); }
        }

        private bool _useOverrideSpeed;
        public bool UseOverrideSpeed
        {
            get => _useOverrideSpeed;
            set
            {
                if (_useOverrideSpeed == value) return;
                _useOverrideSpeed = value;
                OnPropertyChanged();
                if (_eepromService.IsConnected)
                    _eepromService.SetSpeed(_useOverrideSpeed);
            }
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

        public MainViewModel(IFileDialogService fileDialogService, IEepromService eepromService, IEepromFileService fileService, IEepromSerializer eepromSerializer)
        {
            SelectedPort = ConfigurationManager.AppSettings["DefaultPort"] ?? "USB1";
            _selectedAdapterType = GetAppSettingAdapterType("AdapterType", AdapterType.DS9490);
            AllowEditIdentification = GetAppSettingBool("AllowEditIdentification", defaultValue: true);
            AllowEditCalibration = GetAppSettingBool("AllowEditCalibration", defaultValue: true);
            ShowOverdriveCheckbox = GetAppSettingBool("ShowOverdriveCheckbox", defaultValue: false);
            _showProbeUsageDate = GetAppSettingBool("ShowProbeUsageDate", defaultValue: true);
            _eraseFillByte = GetAppSettingByte("EraseFillByte", 0x00);

            _fileDialogService = fileDialogService;
            _eepromService = eepromService;
            _fileService = fileService;
            _eepromSerializer = eepromSerializer;

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
            LoadRawTxtCommand = new RelayCommand(LoadRawTxt);
            SaveJsonCommand = new RelayCommand(SaveJson);
            ExportHexCommand = new RelayCommand(ExportHex);
            SaveRawTxtCommand = new RelayCommand(SaveRawTxt);

            _selectedWriteMode = WriteModeUserData;
            _rawEepromBytes = Array.Empty<byte>();
        }

        public void OnAppClosing()
        {
            _eepromService.Disconnect();
        }

        #region Commands

        private void TogglePort()
        {
            if (IsPortOpen)
            {
                _eepromService.Disconnect();
                IsPortOpen = false;
                ClearUiState();
                ClearRawDataView();
            }
            else if (!string.IsNullOrEmpty(SelectedPort))
            {
                try
                {
                    _eepromService.Connect(SelectedAdapterType, SelectedPort);
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
                    IsPortOpen = false;
                }
            }
        }

        private async Task LoadMemoryAsync()
        {
            IsBusy = true;
            try { await ReadEepromInternalAsync(); }
            finally { IsBusy = false; }
        }

        private async Task WriteMemoryAsync()
        {
            var (dialogTitle, message) = SelectedWriteMode == WriteModeUserData
                ? ("Confirm Write", "Proceed with writing user data to EEPROM?")
                : SelectedWriteMode == WriteModeErase
                    ? ("Confirm Erase", "Erase the entire EEPROM?")
                    : ("Confirm Write", "Proceed with writing entire EEPROM?");

            var dialog = new ConfirmWriteDialog
            {
                Owner = Application.Current?.MainWindow,
                DialogTitle = dialogTitle,
                Message = message
            };
            dialog.DataContext = dialog;

            if (dialog.ShowDialog() != true) return;

            IsBusy = true;
            try
            {
                var mode = SelectedWriteMode == WriteModeErase ? WriteMode.Erase
                    : SelectedWriteMode == WriteModeUserData ? WriteMode.UserDataOnly
                    : WriteMode.Entire;
                await ExecuteWriteAsync(mode);
            }
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
            var bytes = await _eepromService.WriteAsync(_eeprom, mode, _eraseFillByte, progress);
            Progress = 0;
            _rawEepromBytes = (byte[])bytes.Clone();
            HexAsciiText = _fileService.FormatHexAscii(bytes);

            if (mode == WriteMode.Erase)
                ClearUiState();
            else
                ParseEeprom(bytes);
        }

        private void LoadJson()
        {
            var path = _fileDialogService.OpenFile("JSON files|*.json");
            if (path == null) return;

            _eeprom = _fileService.LoadFromJson(path);
            _rawEepromBytes = BuildEepromImage();

            Identification = new IdentificationViewModel(_eeprom.Id, loadFromData: true);
            Calibration = new CalibrationViewModel(_eeprom.Calibration, loadFromData: true);
            User = new UserDataViewModel(_eeprom.User, loadFromData: true);

            OnPropertyChanged(nameof(Identification));
            OnPropertyChanged(nameof(Calibration));
            OnPropertyChanged(nameof(User));
        }

        private void SaveJson()
        {
            var path = _fileDialogService.SaveFile("JSON files|*.json");
            if (path == null) return;
            _fileService.SaveToJson(_eeprom, path);
        }

        private void LoadRawTxt()
        {
            var path = _fileDialogService.OpenFile("Text files|*.txt");
            if (path == null) return;

            try
            {
                var bytes = _fileService.LoadFromRawTxt(path);
                _rawEepromBytes = (byte[])bytes.Clone();
                HexAsciiText = _fileService.FormatHexAscii(bytes);
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
            var path = _fileDialogService.SaveFile("Text files|*.txt");
            if (path == null) return;
            var data = _rawEepromBytes != null && _rawEepromBytes.Length > 0
                ? _rawEepromBytes
                : BuildEepromImage();
            _fileService.SaveToRawTxt(data, path);
        }

        private void ExportHex()
        {
            var path = _fileDialogService.SaveFile("Text files|*.txt");
            if (path == null) return;
            File.WriteAllText(path, HexAsciiText ?? string.Empty);
        }

        #endregion

        #region Helpers

        private bool CanStartOperation() => IsPortOpen && !IsBusy;

        private async Task ReadEepromInternalAsync(bool parse = true)
        {
            var progress = new Progress<int>(p => Progress = p);
            var bytes = await _eepromService.ReadAsync(progress);
            Progress = 0;
            _rawEepromBytes = (byte[])bytes.Clone();
            HexAsciiText = _fileService.FormatHexAscii(bytes);
            if (parse) ParseEeprom(bytes);
        }

        private void ParseEeprom(byte[] raw)
        {
            try
            {
                _eeprom = _eepromSerializer.Decode(raw);

                Identification = new IdentificationViewModel(_eeprom.Id, loadFromData: true);
                Calibration    = new CalibrationViewModel(_eeprom.Calibration, loadFromData: true);
                User           = new UserDataViewModel(_eeprom.User, loadFromData: true);

                OnPropertyChanged(nameof(Identification));
                OnPropertyChanged(nameof(Calibration));
                OnPropertyChanged(nameof(User));
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

        private byte[] BuildEepromImage() => _eepromSerializer.Encode(_eeprom);

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

        private static AdapterType GetAppSettingAdapterType(string key, AdapterType defaultValue)
        {
            var raw = ConfigurationManager.AppSettings[key];
            return Enum.TryParse<AdapterType>(raw, ignoreCase: true, out var v) ? v : defaultValue;
        }

        #endregion
    }
}
