using System.Windows;
using OneWire.Core;
using OneWire.UI.Wpf.ViewModels;

namespace OneWire.UI.Wpf
{
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            IFileDialogService fileDialogService = new FileDialogService();
            IEepromDataManager manager = new EepromDataManager();

            var mainViewModel = new MainViewModel(fileDialogService, manager);

            var mainWindow = new MainWindow { DataContext = mainViewModel };
            mainWindow.Show();
        }
    }
}
