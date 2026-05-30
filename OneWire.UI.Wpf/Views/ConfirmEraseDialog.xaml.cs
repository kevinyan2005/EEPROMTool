using System.Windows;

namespace OneWire.UI.Wpf.Views
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
