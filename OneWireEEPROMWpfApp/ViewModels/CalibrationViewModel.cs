using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using OneWire.Common;

namespace OneWireEEPROMWpfApp.ViewModels
{
    public class CalibrationViewModel : ViewModelBase
    {
        private readonly SensorCalibrationBlock _data;

        public ObservableCollection<GaugeFactorViewModel> GaugeFactors { get; }

        private uint? _referenceValue;
        private DateTime? _manufactureDate;
        private DateTime? _expiryDate;
        private ushort? _crc;

        public CalibrationViewModel(SensorCalibrationBlock data)
            : this(data, false) { }

        public CalibrationViewModel(SensorCalibrationBlock data, bool loadFromData = false)
        {
            _data = data;

            if (loadFromData)
            {
                _referenceValue  = data.ReferenceValue == uint.MaxValue ? (uint?)null : data.ReferenceValue;
                _manufactureDate = data.ManufactureDate == default      ? (DateTime?)null : data.ManufactureDate;
                _expiryDate      = data.ExpiryDate      == default      ? (DateTime?)null : data.ExpiryDate;
                _crc = data.Crc16;
            }
            else
            {
                _referenceValue = null;
                _data.ReferenceValue = uint.MaxValue; // 0xFFFFFFFF = not set
                _manufactureDate = null;
                _expiryDate = null;
                _crc = null;
            }
            

            GaugeFactors = new ObservableCollection<GaugeFactorViewModel>();
            for (int i = 0; i < _data.GaugeFactors.Length; i++)
            {
                GaugeFactors.Add(new GaugeFactorViewModel(i, _data.GaugeFactors, UpdateCrc, loadFromData));
            }
        }
        /// <summary>
        /// Force recalc and notify UI that CRC changed.
        /// </summary>

        public uint? ReferenceValue
        {
            get => _referenceValue;
            set
            {
                if (_referenceValue == value) return;
                _referenceValue = value;
                if (value.HasValue)
                {
                    _data.ReferenceValue = value.Value;
                    UpdateCrc();
                }
                else
                {
                    _data.ReferenceValue = uint.MaxValue; // 0xFFFFFFFF = not set
                    _crc = null;
                    OnPropertyChanged(nameof(Crc));
                }
                OnPropertyChanged();
            }
        }

        public string GaugeType
        {
            get => _data.GaugeType;
            set
            {
                _data.GaugeType = value;
                OnPropertyChanged();
                UpdateCrc();
            }
        }

        public DateTime? ManufactureDate
        {
            get => _manufactureDate;
            set
            {
                if (_manufactureDate == value) return;
                _manufactureDate = value;
                if (value.HasValue)
                {
                    _data.ManufactureDate = value.Value;
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

        public DateTime? ExpiryDate
        {
            get => _expiryDate;
            set
            {
                if (_expiryDate == value) return;
                _expiryDate = value;
                if (value.HasValue)
                {
                    _data.ExpiryDate = value.Value;
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
                    _data.Crc16 = value.Value;
                }
                OnPropertyChanged();
            }
        }

      
        protected void UpdateCrc()
        {
            if (!_referenceValue.HasValue || !_manufactureDate.HasValue || !_expiryDate.HasValue ||
                GaugeFactors.Any(gf => !gf.Value.HasValue))
            {
                _crc = null;
                OnPropertyChanged(nameof(Crc));
                return;
            }

            _data.RecalculateCrc();
            _crc = _data.Crc16;
            OnPropertyChanged(nameof(Crc));
        }
    }


    public class GaugeFactorViewModel : ViewModelBase
    {
        private readonly uint[] _gaugeFactors;
        private readonly Action _updateCrc;

        private int _index;
        public int Index
        {
            get => _index;
            set
            {
                if (_index != value)
                {
                    _index = value;
                    OnPropertyChanged();
                }
            }
        }

        private uint? _value;

        public uint? Value
        {
            get => _value;
            set
            {
                if (_value == value) return;
                _value = value;
                if (value.HasValue)
                {
                    _gaugeFactors[_index] = value.Value; // updates data model
                }
                OnPropertyChanged();
                _updateCrc?.Invoke();
            }
        }

        public GaugeFactorViewModel(int index, uint[] gaugeFactors, Action updateCrc, bool loadFromData)
        {
            _index = index;
            _gaugeFactors = gaugeFactors;
            _updateCrc = updateCrc;

            // If loading, take the value from the array. If not, start as null.
            _value = loadFromData
                ? (gaugeFactors[index] == uint.MaxValue ? (uint?)null : gaugeFactors[index])
                : (uint?)null;
        }

    }
}
