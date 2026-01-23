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
        public IdentificationViewModel(OneWireIdentificationBlock model) => Model = model;

        public ushort DataVersion
        {
            get => Model.DataVersion;
            set
            {
                Model.DataVersion = value;
                OnPropertyChanged();
                UpdateCrc();
            }
        }

        public ushort DataIdent
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

        public ushort Crc
        {
            get => Model.Crc16;
            set
            {
                Model.Crc16 = value;
                OnPropertyChanged();
            }
        }

        /// <summary>
        /// Force recalc and notify UI that CRC changed.
        /// </summary>
        protected void UpdateCrc()
        {
            Model.RecalculateCrc();
            OnPropertyChanged(nameof(Crc));
        }
    }
}
