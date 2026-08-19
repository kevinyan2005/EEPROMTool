using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Collections.ObjectModel;
using System.Linq;
using OneWire.Core;
using slf4net;

namespace OneWire.UI.Wpf.ViewModels
{
    public class UserDataViewModel : ViewModelBase, IDataErrorInfo
    {
        private static ILogger Logger { get; } = LoggerFactory.GetLogger(nameof(UserDataViewModel));

        private readonly UserDefinedBlock _model;
        private ProbeTypeEnum? _selectedProbe;
        private DateTime? _probeUsageDate;
        private DateTime? _probeManufactureDate;
        private ushort? _schema;
        private ushort? _crc;

        private const int LotRequiredLen = 4;
        private const int SequenceRequiredLen = 2;
        private static readonly string[] ProbeLengthOptions = { "00", "000", "1", "2" };

        public UserDataViewModel(UserDefinedBlock model)
            : this(model, false) { }

        public UserDataViewModel(UserDefinedBlock model, bool loadFromData = false)
        {
            _model = model;

            if (loadFromData)
            {
                _schema = model.Schema;
                _probeUsageDate = model.ProbeUsageDate == default ? (DateTime?)null : model.ProbeUsageDate;
                // Not persisted to JSON, so a freshly-loaded model still has its DateTime.MaxValue field default.
                _probeManufactureDate = model.ProbeManufactureDate == default || model.ProbeManufactureDate == DateTime.MaxValue
                    ? (DateTime?)null
                    : model.ProbeManufactureDate;
                _crc = model.Crc16;
                // Initialize the Enum selection based on whatever string is in the model
                SyncSelectedProbeFromPartNumber();
            }
            else
            {
                // Fields stay null, serial number remains empty string in model
                _probeUsageDate = null;
                _model.ProbeUsageDate = DateTime.MaxValue;
                _probeManufactureDate = null;
                _crc = null;
            }
        }

        #region Serial Number Logic

        /// <summary>
        /// This property supports the "Read" from EEPROM.
        /// User.ProbeSerialNumber = Encoding.ASCII.GetString(...).TrimEnd('\0');
        /// </summary>
        public string ProbeSerialNumber
        {
            get => _model.ProbeSerialNumber ?? string.Empty;
            set
            {
                if (_model.ProbeSerialNumber != value)
                {
                    _model.ProbeSerialNumber = value;
                    SyncSelectedProbeFromPartNumber();
                    NotifySerialNumberProperties();
                    UpdateCrc();
                }
            }
        }

        public string PartNumber =>
            _selectedProbe.HasValue ? _selectedProbe.Value.ToPartNumber() : string.Empty;

        /// <summary>
        /// True for FullFire33 and SideFire33 — their serial includes a probe-length segment.
        /// False for FullFire16 — serial is partNum-lot-sequence only.
        /// </summary>
        public bool HasProbeLength =>
            _selectedProbe.HasValue && _selectedProbe.Value.HasProbeLength();

        /// <summary>
        /// Probe length segment (e.g. "1", "2", "5", "00", "000").
        /// Only meaningful when HasProbeLength is true.
        /// </summary>
        public string ProbeLength
        {
            get => HasProbeLength ? SplitParts()[1] : string.Empty;
            set
            {
                if (!HasProbeLength) return;
                var parts = SplitParts();
                parts[1] = value;
                _model.ProbeSerialNumber = BuildProbeSerialNumber(parts, padSequence: false);
                NotifySerialNumberProperties();
                UpdateCrc();
            }
        }

        public string LotNumber
        {
            get
            {
                var parts = SplitParts();
                var lot = HasProbeLength ? parts[2] : parts[1];
                return lot.StartsWith("M") ? lot.Substring(1) : lot;
            }
            set
            {
                var parts = SplitParts();
                int idx = HasProbeLength ? 2 : 1;
                parts[idx] = "M" + (value ?? string.Empty);
                _model.ProbeSerialNumber = BuildProbeSerialNumber(parts, padSequence: false);
                NotifySerialNumberProperties();
                UpdateCrc();
            }
        }

        public string Sequence
        {
            get
            {
                var parts = SplitParts();
                return HasProbeLength ? parts[3] : parts[2];
            }
            set
            {
                var parts = SplitParts();
                int idx = HasProbeLength ? 3 : 2;
                parts[idx] = value;
                _model.ProbeSerialNumber = BuildProbeSerialNumber(parts, padSequence: false);
                NotifySerialNumberProperties();
                UpdateCrc();
            }
        }

        public ProbeTypeEnum? SelectedProbe
        {
            get => _selectedProbe;
            set
            {
                if (_selectedProbe != value)
                {
                    _selectedProbe = value;
                    OnPropertyChanged(nameof(SelectedProbe));
                    OnPropertyChanged(nameof(PartNumber));
                    OnPropertyChanged(nameof(HasProbeLength));
                    RecalculateExpiryDate(); // years offset changes between probe types

                    // Reconstruct the model string because the part number (and format) changed
                    UpdateProbeSerialNumberFromParts();
                    NotifySerialNumberProperties();
                }
            }
        }

        #endregion

        #region Standard Properties

        public ObservableCollection<ProbeTypeEnum> AvailableProbes { get; } =
            new ObservableCollection<ProbeTypeEnum>(Enum.GetValues(typeof(ProbeTypeEnum)).Cast<ProbeTypeEnum>());

        public ObservableCollection<string> AvailableProbeLengths { get; } =
            new ObservableCollection<string>(ProbeLengthOptions);

        // Not included for CRC check
        public DateTime? ProbeUsageDate
        {
            get => _probeUsageDate;
            set
            {
                if (_probeUsageDate == value) return;
                _probeUsageDate = value;
                if (value.HasValue)
                {
                    _model.ProbeUsageDate = value.Value;
                }
                else
                {
                    _model.ProbeUsageDate = DateTime.MaxValue;
                    _crc = null;
                    OnPropertyChanged(nameof(Crc));
                }
                OnPropertyChanged();
            }
        }

        public DateTime? ManufactureDate
        {
            get => _probeManufactureDate;
            set
            {
                if (_probeManufactureDate == value) return;
                _probeManufactureDate = value;
                _model.ProbeManufactureDate = value ?? DateTime.MaxValue;
                RecalculateExpiryDate();
                OnPropertyChanged();
            }
        }

        /// <summary>
        /// Read-only. Calculated as ManufactureDate + 2 years (FullFire16) or + 4 years (FullFire33/SideFire33).
        /// </summary>
        public DateTime? ProbeExpiryDate =>
            _probeManufactureDate.HasValue
                ? _probeManufactureDate.Value.AddYears(HasProbeLength ? 4 : 2)
                : (DateTime?)null;

        public ushort? Schema
        {
            get => _schema;
            set
            {
                if (_model.Schema == value) return;
                _schema = value;
                if (value.HasValue)
                {
                    _model.Schema = value.Value;
                    UpdateCrc();
                }
                else
                {
                    _crc = null;
                    OnPropertyChanged(nameof(Crc));
                }
                OnPropertyChanged();
            }
        }

        public ushort? Crc
        {
            get => _crc;
            set
            {
                _crc = value;
                if (value.HasValue)
                {
                    _model.Crc16 = value.Value;
                }
                OnPropertyChanged();
            }
        }

        protected void UpdateCrc()
        {
            if (!_probeManufactureDate.HasValue)
            {
                _crc = null;
                OnPropertyChanged(nameof(Crc));
                return;
            }

            _model.RecalculateCrc();
            _crc = _model.Crc16;
            OnPropertyChanged(nameof(Crc));
        }

        private void RecalculateExpiryDate()
        {
            var expiry = ProbeExpiryDate;
            _model.ProbeExpiryDate = expiry ?? DateTime.MaxValue;
            OnPropertyChanged(nameof(ProbeExpiryDate));
            UpdateCrc();
        }

        #endregion

        #region Validation

        public string Error => null;

        public string this[string propertyName]
        {
            get
            {
                return propertyName switch
                {
                    nameof(LotNumber) => ValidateLotNumber(LotNumber),
                    nameof(Sequence) => ValidateExactLength(Sequence, SequenceRequiredLen, "Sequence"),
                    _ => null
                };
            }
        }

        private static string ValidateLotNumber(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return "Lot is required.";
            if (value.Length != LotRequiredLen)
                return $"Lot must be {LotRequiredLen} characters.";
            return null;
        }

        private static string ValidateExactLength(string value, int requiredLength, string label)
        {
            if (string.IsNullOrWhiteSpace(value))
                return $"{label} is required.";
            if (value.Length != requiredLength)
                return $"{label} must be {requiredLength} characters.";
            return null;
        }

        #endregion

        #region Helper Methods

        private void SyncSelectedProbeFromPartNumber()
        {
            Logger.Debug("SyncSelectedProbeFromPartNumber called");
            var currentPartNumber = (ProbeSerialNumber ?? string.Empty).Split('-').FirstOrDefault() ?? string.Empty;
            _selectedProbe = ProbeTypeExtensions.FromPartNumber(currentPartNumber);
            OnPropertyChanged(nameof(SelectedProbe));
            OnPropertyChanged(nameof(PartNumber));
            OnPropertyChanged(nameof(HasProbeLength));
        }

        /// <summary>
        /// Called when the probe type changes. Rebuilds the serial number in the correct format
        /// for the new probe type, preserving lot and sequence where possible.
        /// </summary>
        private void UpdateProbeSerialNumberFromParts()
        {
            if (string.IsNullOrEmpty(PartNumber))
                return;

            var raw = (ProbeSerialNumber ?? string.Empty).Split('-');
            string probeLen = string.Empty, lot = string.Empty, seq = string.Empty;

            if (HasProbeLength)
            {
                // New format: partNum-probeLen-lot-seq
                if (raw.Length >= 4)
                {
                    // Was already a 4-part format — keep all components
                    probeLen = raw[1]; lot = raw[2]; seq = raw[3];
                }
                else if (raw.Length == 3)
                {
                    // Was FullFire16 (no probe length) — preserve lot/seq, clear probe length
                    lot = raw[1]; seq = raw[2];
                }
                else if (raw.Length == 2)
                {
                    lot = raw[1];
                }
                if (int.TryParse(seq, out int seqInt))
                    seq = seqInt.ToString("00");
                _model.ProbeSerialNumber = $"{PartNumber}-{probeLen}-{lot}-{seq}";
            }
            else
            {
                // FullFire16 format: partNum-lot-seq (no probe length)
                if (raw.Length >= 4)
                {
                    // Was a 4-part format — drop probe length, keep lot/seq
                    lot = raw[2]; seq = raw[3];
                }
                else if (raw.Length >= 3)
                {
                    lot = raw[1]; seq = raw[2];
                }
                else if (raw.Length >= 2)
                {
                    lot = raw[1];
                }
                if (int.TryParse(seq, out int seqInt))
                    seq = seqInt.ToString("00");
                _model.ProbeSerialNumber = $"{PartNumber}-{lot}-{seq}";
            }
            UpdateCrc();
        }

        private void NotifySerialNumberProperties()
        {
            OnPropertyChanged(nameof(ProbeSerialNumber));
            OnPropertyChanged(nameof(ProbeLength));
            OnPropertyChanged(nameof(LotNumber));
            OnPropertyChanged(nameof(Sequence));
        }

        /// <summary>
        /// Returns parts indexed for the current probe type:
        ///   HasProbeLength → [partNum, probeLen, lot, seq]  (4 elements)
        ///   FullFire16     → [partNum, lot, seq]             (3 elements)
        /// </summary>
        private string[] SplitParts()
        {
            var raw = (ProbeSerialNumber ?? string.Empty).Split('-');

            if (HasProbeLength)
            {
                return new[]
                {
                    raw.ElementAtOrDefault(0) ?? string.Empty,
                    raw.ElementAtOrDefault(1) ?? string.Empty,
                    raw.ElementAtOrDefault(2) ?? string.Empty,
                    raw.ElementAtOrDefault(3) ?? string.Empty,
                };
            }
            else
            {
                return new[]
                {
                    raw.ElementAtOrDefault(0) ?? string.Empty,
                    raw.ElementAtOrDefault(1) ?? string.Empty,
                    raw.ElementAtOrDefault(2) ?? string.Empty,
                };
            }
        }

        private string BuildProbeSerialNumber(string[] parts, bool padSequence)
        {
            if (HasProbeLength)
            {
                var probeLen = parts.Length > 1 ? parts[1] : string.Empty;
                var lot      = parts.Length > 2 ? parts[2] : string.Empty;
                var seq      = parts.Length > 3 ? parts[3] : string.Empty;
                if (padSequence && int.TryParse(seq, out int seqInt))
                    seq = seqInt.ToString("00");
                return $"{PartNumber}-{probeLen}-{lot}-{seq}";
            }
            else
            {
                var lot = parts.Length > 1 ? parts[1] : string.Empty;
                var seq = parts.Length > 2 ? parts[2] : string.Empty;
                if (padSequence && int.TryParse(seq, out int seqInt))
                    seq = seqInt.ToString("00");
                return $"{PartNumber}-{lot}-{seq}";
            }
        }

        #endregion
    }
}
