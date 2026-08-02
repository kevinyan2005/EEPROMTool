using System.Windows;

namespace OneWire.UI.Wpf.Views
{
    public partial class ProgramNextEepromDialog : Window
    {
        public string DialogTitle { get; set; }
        public string Message { get; set; }
        public string WarningMessage { get; set; }

        public ProgramNextEepromDialog()
        {
            DialogTitle = "Program Next EEPROM";
            Message = "Program next EEPROM?";
            WarningMessage = "Verify that the next 1-Wire EEPROM is connected before continuing.";
            InitializeComponent();
            DataContext = this;
        }

        private void Yes_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
        }
    }
}
