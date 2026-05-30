using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using OneWire.UI.Wpf.ViewModels;
using slf4net;

namespace OneWire.UI.Wpf
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private static ILogger Logger { get; } = LoggerFactory.GetLogger(nameof(OneWire.UI.Wpf));

        private static string AppVersion
        {
            get
            {
                var assembly = Assembly.GetExecutingAssembly();
                var fvi = FileVersionInfo.GetVersionInfo(assembly.Location);
                return fvi.FileVersion;
            }
        }

        public MainWindow()
        {
            try
            {
                InitializeComponent();
                Closing += MainWindow_Closing;
            }
            catch (Exception e)
            {
                Logger.Error(e, "Failed to construct MainWindow!");
                throw;
            }
            finally
            {
                Logger.Debug($"Starting Application version {AppVersion}.");
                var procArch = mmi.utils.Process.Is64BitProcess ? "64-bit" : "32-bit";
                var systemArch = mmi.utils.Process.Is64BitOperatingSystem ? "64-bit" : "32-bit";
                Logger.Info($"Process architecture is {procArch}, Windows architecture is {systemArch}");
                Logger.Info($"Framework runtime version is {Environment.Version}");

            }

            Title += $" - {AppVersion}";
            Logger.Debug("OneWire EEPROM Test Tool is running....");
        }

        private void MainWindow_Closing(object sender, CancelEventArgs e)
        {
            if (DataContext is MainViewModel vm)
            {
                // Call your cleanup or logic
                vm.OnAppClosing();
                // Optionally: e.Cancel = vm.ShouldCancelClose;
            }
        }
    }
}
