using System.Windows;
using OneWire.Services;
using OneWireEEPROMWpfApp.ViewModels;

namespace OneWireEEPROMWpfApp
{
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            IFileDialogService fileDialogService = new FileDialogService();
            IEepromService eepromService = new EepromService();
            IEepromFileService fileService = new EepromFileService();

            var mainViewModel = new MainViewModel(fileDialogService, eepromService, fileService);

            var mainWindow = new MainWindow { DataContext = mainViewModel };
            mainWindow.Show();
        }
    }
}
