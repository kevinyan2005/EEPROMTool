using OneWireEEPROMWpfApp.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using OneWire.Common;

namespace OneWireEEPROMWpfApp.ViewModels
{
    public class OneWireIdViewModel : ViewModelBase
    {
        public OneWireIdentificationBlock Model { get; }
        public OneWireIdViewModel(OneWireIdentificationBlock model) => Model = model;

        public ushort DataVersion
        {
            get => Model.DataVersion;
            set
            {
                Model.DataVersion = value;
                OnPropertyChanged();
            }
        }

        public ushort DataIdent
        {
            get => Model.DataId;
            set
            {
                Model.DataId = value;
                OnPropertyChanged();
            }
        }

        public string ChipModel
        {
            get => Model.Model;
            set
            {
                Model.Model = value;
                OnPropertyChanged();
            }
        }

        public string SerialNumber
        {
            get => Model.SerialNumber;
            set
            {
                Model.SerialNumber = value;
                OnPropertyChanged();
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
    }
}
