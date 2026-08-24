using System;
using System.Collections.Generic;
using System.Configuration;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
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
        public ICommand ClearEepromDataCommand { get; }

        private readonly RelayCommand _readEepromCommand;
        private readonly RelayCommand _writeEntireEepromCommand;
        private readonly RelayCommand _writeUserDataCommand;
        private readonly RelayCommand _eraseEepromCommand;

        private EepromData _eeprom;
        private byte[] _rawEepromBytes;
        private readonly byte _eraseFillByte;
        private readonly ushort _userDataVersion;

        private readonly ApplicationMode _mode;
        private readonly bool _showProbeUsageDate;

        public bool AllowEditIdentification => _mode == ApplicationMode.Developer;
        public bool AllowEditCalibration => _mode == ApplicationMode.Developer;
        public bool ShowProbeUsageDate => _showProbeUsageDate;
        public bool ShowWriteEntireEepromButton => _mode == ApplicationMode.Developer;
        public bool ShowFormatEepromButton => _mode == ApplicationMode.Developer;
        public bool ShowIdentificationCrcCheckbox => _mode == ApplicationMode.Developer;
        public bool ShowCalibrationCrcCheckbox => _mode == ApplicationMode.Developer;
        public bool ShowUserCrcCheckbox => _mode == ApplicationMode.Developer;

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
            _mode = GetAppSettingApplicationMode("ApplicationMode", ApplicationMode.Production);
            _showProbeUsageDate = GetAppSettingBool("ShowProbeUsageDate", defaultValue: true);
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
            ClearEepromDataCommand = new RelayCommand(ClearEepromData);

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

            AutoSaveJsonAfterRead();
        }

        /// <summary>
        /// Automatically archives the just-read EEPROM image to a JSON file in <see cref="OutputFilesDirectory"/>
        /// so every read is captured on disk without requiring a manual "Save JSON" action.
        /// </summary>
        private void AutoSaveJsonAfterRead()
        {
            var path = Path.Combine(OutputFilesDirectory, BuildDefaultFileName() + ".json");
            TrySaveEepromToJson(_eeprom, path, "Save Image (JSON)");
        }

        /// <summary>
        /// Reads the EEPROM's raw contents into a standalone snapshot without touching the live view
        /// models or the in-memory <see cref="_eeprom"/> — used to archive before/after state around a
        /// write without disturbing pending edits or the currently displayed data.
        /// </summary>
        private async Task<(bool success, EepromData snapshot)> ReadEepromSnapshotAsync(string historyOperationName)
        {
            var stopwatch = Stopwatch.StartNew();
            var result = "Success";
            EepromData snapshot = null;
            var success = true;
            try
            {
                var progress = new Progress<int>(p => Progress = p);
                var bytes = await _manager.ReadRawAsync(progress);
                Progress = 0;

                try
                {
                    snapshot = _manager.Decode(bytes, BuildCrcCheckOptions());
                }
                catch (CrcValidationException ex)
                {
                    snapshot = ex.EepromData;
                    result = "Success (CRC check failed)";
                }
            }
            catch (Exception ex)
            {
                result = $"Failed: {ex.Message}";
                success = false;
                Logger.Error(ex, $"{historyOperationName} failed.");
            }
            finally
            {
                stopwatch.Stop();
                History.Add(historyOperationName, BuildDeviceLabel(), result, stopwatch.Elapsed);
            }
            return (success, snapshot);
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
                var success = await WriteMemoryWithSnapshotsAsync(mode, operationName);
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

            await WriteMemoryWithSnapshotsAsync(WriteMode.Erase, "Format EEPROM");
        }

        /// <summary>
        /// Wraps a hardware write with before/after JSON snapshots for audit purposes: reads the
        /// EEPROM's current contents and archives them as "&lt;name&gt;_before.json" prior to writing,
        /// executes the write, then — only if it succeeded — reads back and archives "&lt;name&gt;_after.json".
        /// The pre-write read captures a standalone snapshot rather than refreshing the live view models,
        /// since doing so would overwrite the pending edits this operation is about to write.
        /// </summary>
        private async Task<bool> WriteMemoryWithSnapshotsAsync(WriteMode mode, string operationName)
        {
            IsBusy = true;
            try
            {
                if (mode != WriteMode.Erase)
                {
                    var validationErrors = ValidateProbeDataBeforeEepromWrite();
                    if (validationErrors.Count > 0)
                    {
                        HandleDataIntegrityValidationFailure(operationName, validationErrors);
                        return false;
                    }
                }

                var baseName = BuildDefaultFileName();

                var (beforeReadOk, beforeSnapshot) = await ReadEepromSnapshotAsync($"Read EEPROM (Before {operationName})");
                if (!beforeReadOk)
                {
                    MessageBox.Show(
                        $"Could not read the EEPROM before writing. {operationName} was cancelled.",
                        "Write Cancelled",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                    return false;
                }
                TrySaveEepromToJson(beforeSnapshot, BuildSnapshotPath(baseName, "before"), $"Save Image (JSON) - Before {operationName}");

                if (!await WriteMemoryTrackedAsync(mode, operationName))
                    return false;

                var (afterReadOk, afterSnapshot) = await ReadEepromSnapshotAsync($"Read EEPROM (After {operationName})");
                if (afterReadOk)
                    TrySaveEepromToJson(afterSnapshot, BuildSnapshotPath(baseName, "after"), $"Save Image (JSON) - After {operationName}");

                return true;
            }
            finally
            {
                IsBusy = false;
            }
        }

        private static string BuildSnapshotPath(string baseName, string suffix) =>
            Path.Combine(OutputFilesDirectory, $"{baseName}_{suffix}.json");

        /// <summary>
        /// Validates the in-memory UserData block against required invariants before it is
        /// committed to the EEPROM. Returns an empty list when every check passes.
        /// </summary>
        private List<string> ValidateProbeDataBeforeEepromWrite()
        {
            const ushort requiredVersion = 1;
            var user = _eeprom.User;
            var errors = new List<string>();

            if (user.Schema != requiredVersion)
                errors.Add($"UserData.Version must equal {requiredVersion} (current value: {user.Schema}).");

            if (string.IsNullOrWhiteSpace(user.ProbeSerialNumber))
                errors.Add("ProbeSerialNumber must not be null, empty, or whitespace.");

            if (IsUninitializedProbeExpiryDate(user.ProbeExpiryDate))
                errors.Add("ProbeExpiryDate must not be the uninitialized value (9999-12-31T23:59:59).");

            return errors;
        }

        private static bool IsUninitializedProbeExpiryDate(DateTime expiry) =>
            expiry.Year == 9999 && expiry.Month == 12 && expiry.Day == 31 &&
            expiry.Hour == 23 && expiry.Minute == 59 && expiry.Second == 59;

        private void HandleDataIntegrityValidationFailure(string operationName, List<string> errors)
        {
            var details = string.Join(Environment.NewLine, errors.Select(e => "- " + e));
            Logger.Error($"Data integrity validation failed for {operationName}: {string.Join(" | ", errors)}");

            MessageBox.Show(
                $"{operationName} was cancelled because the following data integrity check(s) failed:{Environment.NewLine}{Environment.NewLine}{details}",
                "Data Integrity Check Failed",
                MessageBoxButton.OK,
                MessageBoxImage.Error);

            History.Add(
                $"{operationName} (Aborted)",
                BuildDeviceLabel(),
                $"Failed: Data integrity validation failed - {string.Join("; ", errors)}",
                TimeSpan.Zero);
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

            SaveEepromToJsonOrThrow(_eeprom, path, "Save Image (JSON)");
        }

        /// <summary>
        /// Core JSON export used by every save path (manual Save JSON, auto-save-after-read, and the
        /// write workflow's before/after snapshots): saves, times, logs, and records a History entry,
        /// then rethrows so a user-initiated save can surface the failure.
        /// </summary>
        private void SaveEepromToJsonOrThrow(EepromData data, string path, string historyLabel)
        {
            var stopwatch = Stopwatch.StartNew();
            var result = "Success";
            try
            {
                _manager.SaveToJson(data, path);
            }
            catch (Exception ex)
            {
                result = $"Failed: {ex.Message}";
                Logger.Error(ex, $"Failed to save EEPROM JSON file ({historyLabel}).");
                throw;
            }
            finally
            {
                stopwatch.Stop();
                History.Add(historyLabel, BuildDeviceLabel(), result, stopwatch.Elapsed);
            }
        }

        /// <summary>Non-throwing wrapper for automatic/background saves that must not interrupt their caller.</summary>
        private bool TrySaveEepromToJson(EepromData data, string path, string historyLabel)
        {
            try
            {
                SaveEepromToJsonOrThrow(data, path, historyLabel);
                return true;
            }
            catch
            {
                return false;
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

        private void ClearEepromData()
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

        private static ApplicationMode GetAppSettingApplicationMode(string key, ApplicationMode defaultValue)
        {
            var raw = ConfigurationManager.AppSettings[key];
            return Enum.TryParse<ApplicationMode>(raw, ignoreCase: true, out var v) ? v : defaultValue;
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
