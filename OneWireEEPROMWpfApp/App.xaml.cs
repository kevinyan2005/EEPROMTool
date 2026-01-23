using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using OneWireEEPROMWpfApp.ViewModels;

namespace OneWireEEPROMWpfApp
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            // Setup services
            IFileDialogService fileDialogService = new FileDialogService();

            // Create ViewModel
            var mainViewModel = new MainViewModel(fileDialogService);

            // Create MainWindow and assign DataContext
            var mainWindow = new MainWindow
            {
                DataContext = mainViewModel
            };

            mainWindow.Show();
        }
    }
}
