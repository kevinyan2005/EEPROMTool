using OneWire.Core;

namespace OneWire.UI.Wpf.ViewModels
{
    public class IdentificationViewModel : ViewModelBase
    {
        public OneWireIdentificationBlock Model { get; }
        private ushort? _dataVersion;
        private ushort? _crc;


        public IdentificationViewModel(OneWireIdentificationBlock model)
            : this(model, false) { }

        public IdentificationViewModel(OneWireIdentificationBlock model, bool loadFromData = false)
        {
            Model = model;
            if (loadFromData)
            {
                _dataVersion = model.DataVersion;
                _crc = model.Crc16;
            }
            else
            {
                _dataVersion = null;
                _crc = null;
            }
        }

        public ushort? DataVersion
        {
            get => _dataVersion;
            set
            {
                if (_dataVersion == value) return;
                _dataVersion = value;
                if (value.HasValue)
                {
                    Model.DataVersion = value.Value;
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

        public string DataIdent
        {
            get => Model.DataId;
            set
            {
                Model.DataId = value;
                OnPropertyChanged();
                UpdateCrc();
            }
        }

        public string ChipModel
        {
            get => Model.Model;
            set
            {
                Model.Model = value;
                OnPropertyChanged();
                UpdateCrc();
            }
        }

        public string SerialNumber
        {
            get => Model.SerialNumber;
            set
            {
                Model.SerialNumber = value;
                OnPropertyChanged();
                UpdateCrc();
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
                    Model.Crc16 = value.Value;
                }
                OnPropertyChanged();
            }
        }

        /// <summary>
        /// Force recalc and notify UI that CRC changed.
        /// </summary>
        protected void UpdateCrc()
        {
            if (!_dataVersion.HasValue)
            {
                _crc = null;
                OnPropertyChanged(nameof(Crc));
                return;
            }

            Model.RecalculateCrc();
            _crc = Model.Crc16;
            OnPropertyChanged(nameof(Crc));
        }
    }
}
