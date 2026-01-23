using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using OneWire.Common;

namespace OneWireEEPROMWpfApp.ViewModels
{
    public class UserDataViewModel : ViewModelBase
    {
        private readonly UserDefinedBlock _model;

        public static readonly IReadOnlyDictionary<ProbeTypeEnum, string> TypeToPartNumber =
            new Dictionary<ProbeTypeEnum, string>
            {
                { ProbeTypeEnum.FullFire33, "20893" },
                { ProbeTypeEnum.FullFire16, "22022" },
                { ProbeTypeEnum.SideFire33, "20891" },
            };

        public UserDataViewModel(UserDefinedBlock model)
        {
            _model = model;

            // Default selection(optional)
            SelectedProbe = AvailableProbes.FirstOrDefault();

        }

        // SN example: PartNumber - LotNumber - SequenceNumber (16 bytes totally)
        // PartNumber: 5
        // LotNumber: 5
        // SequenceNumber: 4
        public string ProbeSerialNumber
        {
            get => _model.ProbeSerialNumber ?? string.Empty;
            set
            {
                if (_model.ProbeSerialNumber != value)
                {
                    _model.ProbeSerialNumber = value;

                    // When the string is set (from EEPROM), update the Enum selection
                    SyncSelectedProbeFromPartNumber();

                    OnPropertyChanged(nameof(ProbeSerialNumber));
                    OnPropertyChanged(nameof(PartNumber));
                    OnPropertyChanged(nameof(LotNumber));
                    OnPropertyChanged(nameof(Sequence));
                }
            }
        }

        private void SyncSelectedProbeFromPartNumber()
        {
            var currentPartNumber = SplitParts()[0];

            // Look up the Enum that matches the Part Number string
            var match = TypeToPartNumber.FirstOrDefault(x => x.Value == currentPartNumber);

            // If found, update the backing field without triggering the "Write" logic again
            if (match.Value != null)
            {
                _selectedProbe = match.Key;
                OnPropertyChanged(nameof(SelectedProbe));
            }
        }

        //public string ProbeSerialNumber => $"{PartNumber}-{LotNumber}-{Sequence:0000}";

        //public string ProbeSerialNumber
        //{
        //    get => _model.ProbeSerialNumber;
        //    set
        //    {
        //        if (_model.ProbeSerialNumber != value)
        //        {
        //            _model.ProbeSerialNumber = value;
        //            OnPropertyChanged();
        //            OnPropertyChanged(nameof(PartNumber));
        //            OnPropertyChanged(nameof(LotNumber));
        //            OnPropertyChanged(nameof(Sequence));
        //        }
        //    }
        //}

        public DateTime ExpiryDate
        {
            get => _model.ProbeManufactureDate;
            set
            {
                _model.ProbeManufactureDate = value;
                OnPropertyChanged();
                UpdateCrc();
            }
        }

        public DateTime ManufactureDate
        {
            get => _model.ProbeManufactureDate;
            set
            {
                _model.ProbeManufactureDate = value;
                OnPropertyChanged();
                UpdateCrc();
            }
        }

        public DateTime ProbeUsage
        {
            get => _model.FirstConnectionDate;
            set
            {
                _model.FirstConnectionDate = value;
                OnPropertyChanged();
                UpdateCrc();
            }
        }


        public uint ZeroValue
        {
            get => _model.ZeroValue;
            set
            {
                _model.ZeroValue = value;
                OnPropertyChanged();
                UpdateCrc();
            }
        }

        public uint EqualizationFactor
        {
            get => _model.EqualizationFactor;
            set
            {
                _model.EqualizationFactor = value;
                OnPropertyChanged();
                UpdateCrc();
            }
        }

        public ushort Crc
        {
            get => _model.Crc16;
            set
            {
                _model.Crc16 = value;
                OnPropertyChanged();
            }
        }

        public string PartNumber =>
            TypeToPartNumber.TryGetValue(SelectedProbe, out var pn)
                ? pn
                : string.Empty;

        public ObservableCollection<ProbeTypeEnum> AvailableProbes { get; } =
            new ObservableCollection<ProbeTypeEnum>(Enum.GetValues(typeof(ProbeTypeEnum)).Cast<ProbeTypeEnum>());


        private ProbeTypeEnum _selectedProbe;
        public ProbeTypeEnum SelectedProbe
        {
            get => _selectedProbe;
            set
            {
                if (_selectedProbe != value)
                {
                    _selectedProbe = value;
                    OnPropertyChanged(nameof(SelectedProbe));
                    OnPropertyChanged(nameof(PartNumber)); // notify that derived value changed

                    UpdateModelSerialNumberFromParts();
                    OnPropertyChanged(ProbeSerialNumber);
                }
            }
        }

        public string LotNumber
        {
            get => SplitParts()[1];
            set => UpdateSerialNumber(1, value);
        }

        public string Sequence
        {
            get => SplitParts()[2];
            set => UpdateSerialNumber(2, value);
        }


        /// <summary>
        /// Force recalc and notify UI that CRC changed.
        /// </summary>
        protected void UpdateCrc()
        {
            _model.RecalculateCrc();
            OnPropertyChanged(nameof(Crc));
        }

        private string[] SplitParts()
        {
            // Always return 3 elements
            var parts = (ProbeSerialNumber ?? string.Empty).Split('-');
            return new string[]
            {
                parts.ElementAtOrDefault(0) ?? string.Empty,
                parts.ElementAtOrDefault(1) ?? string.Empty,
                parts.ElementAtOrDefault(2) ?? string.Empty,
            };
        }

        private void UpdateSerialNumber(int index, string value)
        {
            var parts = SplitParts();
            parts[index] = value;
            _model.ProbeSerialNumber = string.Join("-", parts);
            OnPropertyChanged(nameof(ProbeSerialNumber));
            OnPropertyChanged(nameof(PartNumber));
            OnPropertyChanged(nameof(LotNumber));
            OnPropertyChanged(nameof(Sequence));
            UpdateCrc();
        }

        //private void UpdateModelSerialNumberFromParts()
        //{
        //    // 1. Get the current components (Lot and Sequence) from the existing string
        //    var parts = SplitParts();

        //    // 2. Construct the new string using the fresh PartNumber 
        //    // and the existing Lot (parts[1]) and Sequence (parts[2])
        //    string newFullSerialNumber = $"{PartNumber}-{parts[1]}-{parts[2]}";

        //    // 3. Update the model
        //    _model.ProbeSerialNumber = newFullSerialNumber;

        //    // 4. Recalculate CRC because the data string has changed
        //    UpdateCrc();
        //}

        // Helper method to keep logic DRY (Don't Repeat Yourself)
        private void UpdateModelSerialNumberFromParts()
        {
            // This takes the CURRENT PartNumber (from the dropdown) + existing Lot/Sequence
            // and pushes it into the _model.
            var parts = SplitParts();
            // parts[0] is the old part number, we replace it with the new one:
            _model.ProbeSerialNumber = $"{PartNumber}-{parts[1]}-{parts[2]}";
            UpdateCrc();
        }
    }
}
