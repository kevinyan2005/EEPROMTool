using System.Windows;

namespace OneWireEEPROMWpfApp.Views
{
    public partial class ConfirmWriteDialog : Window
    {
        public ConfirmWriteDialog()
        {
            InitializeComponent();
        }

        private void Confirm_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
        }
    }
}
