using System.Windows;

namespace OneWire.UI.Wpf.Views
{
    public partial class ConfirmWriteDialog : Window
    {
        public string Message { get; set; }
        public string DialogTitle { get; set; }

        public ConfirmWriteDialog()
        {
            Message = "Proceed with writing data to EEPROM?";
            DialogTitle = "Confirm Write";
            InitializeComponent();
            DataContext = this;
        }

        private void Confirm_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
        }
    }
}
