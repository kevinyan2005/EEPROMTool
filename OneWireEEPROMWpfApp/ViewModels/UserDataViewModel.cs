using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Collections.ObjectModel;
using System.Linq;
using OneWire.Common;
using slf4net;

namespace OneWireEEPROMWpfApp.ViewModels
{
    public class UserDataViewModel : ViewModelBase, IDataErrorInfo
    {
        private static ILogger Logger { get; } = LoggerFactory.GetLogger(nameof(UserDataViewModel));

        private readonly UserDefinedBlock _model;
        private ProbeTypeEnum? _selectedProbe;
        private DateTime? _probeUsageDate;
        private DateTime? _probeExpiryDate;
        private ushort? _schema;
        private ushort? _crc;

        private const int LenRequiredLen = 1;
        private const int LotRequiredLen = 5;
        private const int SequenceRequiredLen = 2;


        public static readonly IReadOnlyDictionary<ProbeTypeEnum, string> TypeToPartNumber =
            new Dictionary<ProbeTypeEnum, string>
            {
                { ProbeTypeEnum.FullFire33, "20893" },
                { ProbeTypeEnum.FullFire16, "22022" },
                { ProbeTypeEnum.SideFire33, "20891" },
            };

        public UserDataViewModel(UserDefinedBlock model)
            : this(model, false) { }

        public UserDataViewModel(UserDefinedBlock model, bool loadFromData = false)
        {
            _model = model;

            if (loadFromData)
            {
                _schema = model.Schema;
                _probeUsageDate = model.ProbeUsageDate == default ? (DateTime?)null : model.ProbeUsageDate;
                _probeExpiryDate = model.ProbeExpiryDate == default ? (DateTime?)null : model.ProbeExpiryDate;
                _crc = model.Crc16;
                // Initialize the Enum selection based on whatever string is in the model
                SyncSelectedProbeFromPartNumber();
            }
            else
            {
                // Fields stay null, serial number remains empty string in model
                _probeUsageDate = null;
                _probeExpiryDate = null;
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
            _selectedProbe.HasValue && TypeToPartNumber.TryGetValue(_selectedProbe.Value, out var pn)
                ? pn
                : string.Empty;

        public string SizeNumber
        {
            get => SplitParts()[1];
            set => UpdateProbeSerialNumberComponent(1, value);
        }

        public string LotNumber
        {
            get => SplitParts()[2];
            set => UpdateProbeSerialNumberComponent(2, value);
        }

        public string Sequence
        {
            get => SplitParts()[3];
            set => UpdateProbeSerialNumberComponent(3, value);
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

                    // Reconstruct the model string because the Part Number changed
                    UpdateProbeSerialNumberFromParts();
                    NotifySerialNumberProperties();
                }
            }
        }

        #endregion

        #region Standard Properties

        public ObservableCollection<ProbeTypeEnum> AvailableProbes { get; } =
            new ObservableCollection<ProbeTypeEnum>(Enum.GetValues(typeof(ProbeTypeEnum)).Cast<ProbeTypeEnum>());

        // Not included for CRC check
        public DateTime? ProbeUsageDate
        {
            get => _probeUsageDate; // Using same model field as your example
            set
            {
                if (_probeUsageDate == value) return;
                _probeUsageDate = value;
                if (value.HasValue)
                {
                    _model.ProbeExpiryDate = value.Value;
                    //UpdateCrc();
                }
                else
                {
                    _crc = null;
                    OnPropertyChanged(nameof(Crc));
                }
                OnPropertyChanged();
            }
        }


        public DateTime? ProbeExpiryDate
        {
            get => _probeExpiryDate; // Using same model field as your example
            set
            {
                if (_probeExpiryDate == value) return;
                _probeExpiryDate = value;
                if (value.HasValue)
                {
                    _model.ProbeExpiryDate = value.Value;
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
            //if (!_probeUsageDate.HasValue || !_probeExpiryDate.HasValue)
            if (!_probeExpiryDate.HasValue)
            {
                _crc = null;
                OnPropertyChanged(nameof(Crc));
                return;
            }

            _model.RecalculateCrc();
            _crc = _model.Crc16;
            OnPropertyChanged(nameof(Crc));
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
                    nameof(SizeNumber) => ValidateExactLength(SizeNumber, LenRequiredLen, "Len"),
                    nameof(LotNumber) => ValidateExactLength(LotNumber, LotRequiredLen, "Lot"),
                    nameof(Sequence) => ValidateExactLength(Sequence, SequenceRequiredLen, "Sequence"),
                    _ => null
                };
            }
        }

        private static string ValidateExactLength(string value, int requiredLength, string label)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return $"{label} is required.";
            }

            if (value.Length != requiredLength)
            {
                return $"{label} must be {requiredLength} characters.";
            }

            return null;
        }

        #endregion

        #region Helper Methods
        /// <summary>
        /// Support Bi-directional sync. Handles the case where you read the raw bytes
        /// </summary>
        private void SyncSelectedProbeFromPartNumber()
        {
            Logger.Debug("SyncSelectedProbeFromPartNumber called");
            var currentPartNumber = SplitParts()[0];
            var match = TypeToPartNumber.FirstOrDefault(x => x.Value == currentPartNumber);

            if (match.Value != null)
            {
                _selectedProbe = match.Key;
            }
            else
            {
                _selectedProbe = null;
            }

            OnPropertyChanged(nameof(SelectedProbe));
            OnPropertyChanged(nameof(PartNumber));
        }

        private void UpdateProbeSerialNumberFromParts()
        {
            if (string.IsNullOrEmpty(PartNumber))
            {
                return;
            }

            var parts = SplitParts();
            _model.ProbeSerialNumber = BuildProbeSerialNumber(parts, padSequence: true);
            UpdateCrc();
        }

        private void UpdateProbeSerialNumberComponent(int index, string value)
        {
            var parts = SplitParts();
            parts[index] = value;

            // Rebuild the full string to save to model
            // Note: We use the enum-based PartNumber for index 0 to stay in sync
            _model.ProbeSerialNumber = BuildProbeSerialNumber(parts, padSequence: false);

            NotifySerialNumberProperties();
            UpdateCrc();
        }

        private void NotifySerialNumberProperties()
        {
            OnPropertyChanged(nameof(ProbeSerialNumber));
            OnPropertyChanged(nameof(SizeNumber));
            OnPropertyChanged(nameof(LotNumber));
            OnPropertyChanged(nameof(Sequence));
        }

        private string[] SplitParts()
        {
            var parts = (ProbeSerialNumber ?? string.Empty).Split('-');
            return new string[]
            {
                parts.ElementAtOrDefault(0) ?? string.Empty,
                parts.ElementAtOrDefault(1) ?? string.Empty,
                parts.ElementAtOrDefault(2) ?? string.Empty,
                parts.ElementAtOrDefault(3) ?? string.Empty,
            };
        }

        private string BuildProbeSerialNumber(string[] parts, bool padSequence)
        {
		    // Index 3 (Sequence) is padded to 2 digits
            var sequence = parts[3];

            if (padSequence && int.TryParse(sequence, out int seqInt))
            {
                sequence = seqInt.ToString("00");
            }

            return $"{PartNumber}-{parts[1]}-{parts[2]}-{sequence}";
        }

        #endregion
  
    }
}
