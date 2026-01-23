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

        public uint ReferenceValue
        {
            get => _data.ReferenceValue;
            set
            {
                _data.ReferenceValue = value;
                OnPropertyChanged();
                UpdateCrc();
            }
        }

        //public ushort GaugeType
        //{
        //    get => _data.GaugeType;
        //    set
        //    {
        //        _data.GaugeType = value;
        //        OnPropertyChanged();
        //        UpdateCrc();
        //    }
        //}
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

        public DateTime ManufactureDate
        {
            get => _data.ManufactureDate;
            set
            {
                _data.ManufactureDate = value;
                OnPropertyChanged();
                UpdateCrc();
            }
        }

        public DateTime ExpiryDate
        {
            get => _data.ExpiryDate;
            set
            {
                _data.ExpiryDate = value;
                OnPropertyChanged();
                UpdateCrc();
            }
        }

        public ushort Crc
        {
            get => _data.Crc16;
            set
            {
                _data.Crc16 = value;
                OnPropertyChanged();
            }
        }

        public CalibrationViewModel(SensorCalibrationBlock data)
        {
            _data = data;

            GaugeFactors = new ObservableCollection<GaugeFactorViewModel>();
            for (int i = 0; i < _data.GaugeFactors.Length; i++)
            {
                GaugeFactors.Add(new GaugeFactorViewModel(i, _data.GaugeFactors, UpdateCrc));
            }
        }
        /// <summary>
        /// Force recalc and notify UI that CRC changed.
        /// </summary>
        protected void UpdateCrc()
        {
            _data.RecalculateCrc();
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

        public uint Value
        {
            get => _gaugeFactors[_index];
            set
            {
                if (_gaugeFactors[_index] != value)
                {
                    _gaugeFactors[_index] = value; // updates data model
                    OnPropertyChanged();
                    _updateCrc?.Invoke();
                }
            }
        }

        public GaugeFactorViewModel(int index, uint[] gaugeFactors, Action updateCrc)
        {
            _index = index;
            _gaugeFactors = gaugeFactors;
            _updateCrc = updateCrc;
        }

    }
}
