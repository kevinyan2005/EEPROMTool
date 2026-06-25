using System;
using System.Configuration;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using OneWire.Adapters;
using OneWire.Common;
using OneWire.Core;
using slf4net;

namespace OneWire.UI.Wpf.ViewModels
{
    public class ConnectivityViewModel : ViewModelBase
    {
        private static ILogger Logger { get; } = LoggerFactory.GetLogger(nameof(ConnectivityViewModel));

        private readonly IEepromDataManager _manager;
        private readonly AdapterType _selectedAdapterType;
        private readonly Action _onDisconnected;

        private string _selectedPort;
        private string[] _availablePorts = Array.Empty<string>();
        private bool _isPortOpen;
        private bool _showOverdriveCheckbox;
        private bool _useOverrideSpeed;

        public ConnectivityViewModel(IEepromDataManager manager, Action onDisconnected)
        {
            _manager = manager;
            _onDisconnected = onDisconnected;

            _selectedAdapterType = GetAppSettingAdapterType("AdapterType", AdapterType.DS9490);
            RefreshAvailablePorts();
            SelectedPort = _selectedAdapterType == AdapterType.DS9490
                ? "USB1"
                : AvailablePorts.FirstOrDefault() ?? string.Empty;
            ShowOverdriveCheckbox = GetAppSettingBool("ShowOverdriveCheckbox", defaultValue: false);

            TogglePortCommand = new RelayCommand(TogglePort);
            RefreshPortsCommand = new RelayCommand(RefreshAvailablePorts);
        }

        public ICommand RefreshPortsCommand { get; }
        public ICommand TogglePortCommand { get; }

        public string SelectedPort
        {
            get => _selectedPort;
            set
            {
                if (_selectedPort == value) return;
                _selectedPort = value;
                OnPropertyChanged();
            }
        }

        public string[] AvailablePorts
        {
            get => _availablePorts;
            private set
            {
                _availablePorts = value;
                OnPropertyChanged();
            }
        }

        public AdapterType SelectedAdapterType => _selectedAdapterType;
        public bool IsPortRequired => _selectedAdapterType != AdapterType.Mock;

        public bool IsPortOpen
        {
            get => _isPortOpen;
            private set
            {
                if (_isPortOpen == value) return;
                _isPortOpen = value;
                OnPropertyChanged();
            }
        }

        public bool ShowOverdriveCheckbox
        {
            get => _showOverdriveCheckbox;
            private set
            {
                if (_showOverdriveCheckbox == value) return;
                _showOverdriveCheckbox = value;
                OnPropertyChanged();
            }
        }

        public bool UseOverrideSpeed
        {
            get => _useOverrideSpeed;
            set
            {
                if (_useOverrideSpeed == value) return;
                _useOverrideSpeed = value;
                OnPropertyChanged();
                if (_manager.IsConnected)
                    _manager.SetSpeed(_useOverrideSpeed);
            }
        }

        public void Close()
        {
            if (!IsPortOpen) return;
            _manager.Close();
            IsPortOpen = false;
        }

        private void TogglePort()
        {
            if (IsPortOpen)
            {
                _manager.Close();
                IsPortOpen = false;
                _onDisconnected?.Invoke();
                return;
            }

            if (string.IsNullOrEmpty(SelectedPort))
                return;

            try
            {
                var adapter = OneWireAdapterFactory.Create(SelectedAdapterType, SelectedPort);
                _manager.Open(adapter);
                IsPortOpen = true;
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Failed to connect to 1-Wire adapter.");
                MessageBox.Show(
                    "1-Wire adapter not available. Connect the USB adapter and try again.",
                    "Connection Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
                IsPortOpen = false;
            }
        }

        private void RefreshAvailablePorts()
        {
            switch (_selectedAdapterType)
            {
                case AdapterType.DS9490:
                    AvailablePorts = new[] { "USB1" };
                    break;
                case AdapterType.Mock:
                    AvailablePorts = Array.Empty<string>();
                    break;
                default:
                    var detected = SerialPortDetector.AutoDetectActiveSerialPorts() ?? Array.Empty<string>();
                    AvailablePorts = detected
                        .Select(s =>
                        {
                            var i = s.IndexOf(':');
                            return i > 0 ? s.Substring(0, i).Trim() : s.Trim();
                        })
                        .ToArray();
                    break;
            }
        }

        private static bool GetAppSettingBool(string key, bool defaultValue)
        {
            var raw = ConfigurationManager.AppSettings[key];
            return bool.TryParse(raw, out var v) ? v : defaultValue;
        }

        private static AdapterType GetAppSettingAdapterType(string key, AdapterType defaultValue)
        {
            var raw = ConfigurationManager.AppSettings[key];
            return Enum.TryParse<AdapterType>(raw, ignoreCase: true, out var v) ? v : defaultValue;
        }
    }
}
