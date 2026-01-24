using OneWireEEPROMWpfApp.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using OneWire.Common;

namespace OneWireEEPROMWpfApp.ViewModels
{
    public class IdentificationViewModel : ViewModelBase
    {
        public OneWireIdentificationBlock Model { get; }

        
       
        private ushort? _crc;

        public IdentificationViewModel(OneWireIdentificationBlock model)
        {
            Model = model;
            _dataVersion = model.DataVersion == 0 ? (ushort?)null : model.DataVersion;
            _dataIdent = model.DataId == 0 ? (ushort?)null : model.DataId;
            _crc = model.Crc16 == 0 ? (ushort?)null : model.Crc16;
        }

        private ushort? _dataVersion;
        public ushort? DataVersion
        {
            get => _dataVersion;
            set
            {
               _dataVersion = value;
                OnPropertyChanged(nameof(DataVersion));
                UpdateCrc();
            }
        }

        private ushort? _dataIdent;
        public ushort? DataIdent
        {
            get => _dataIdent;
            set
            {
                _dataIdent = value;
                OnPropertyChanged(nameof(DataIdent));                
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
            if (!_dataVersion.HasValue || !_dataIdent.HasValue)
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
