using System.Windows;

namespace OneWireEEPROMWpfApp.Views
{
    public partial class ConfirmEraseDialog : Window
    {
        public ConfirmEraseDialog()
        {
            InitializeComponent();
        }

        private void Confirm_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
        }
    }
}
