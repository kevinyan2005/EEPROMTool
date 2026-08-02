using System;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Input;
using slf4net;

namespace OneWire.UI.Wpf.ViewModels
{
    public class OperationHistoryViewModel : ViewModelBase
    {
        private static ILogger Logger { get; } = LoggerFactory.GetLogger(nameof(OperationHistoryViewModel));

        private readonly IFileDialogService _fileDialogService;

        public ObservableCollection<OperationHistoryEntry> Entries { get; } = new ObservableCollection<OperationHistoryEntry>();

        public ICommand ExportCsvCommand { get; }

        public OperationHistoryViewModel(IFileDialogService fileDialogService)
        {
            _fileDialogService = fileDialogService;
            ExportCsvCommand = new RelayCommand(ExportCsv);
        }

        public void Add(string operation, string device, string result, TimeSpan duration)
        {
            Entries.Add(new OperationHistoryEntry
            {
                Timestamp = DateTime.Now,
                Operation = operation,
                Device = device,
                Result = result,
                Duration = duration
            });
        }

        private void ExportCsv()
        {
            var defaultFileName = $"history_{DateTime.Now:yyyy-MM-dd_HH-mm-ss}.csv";
            var path = _fileDialogService.SaveFile("CSV files|*.csv", defaultFileName);
            if (path == null) return;

            try
            {
                var sb = new StringBuilder();
                sb.AppendLine("Timestamp,Operation,Device,Result,Duration");
                foreach (var entry in Entries)
                {
                    sb.AppendLine(string.Join(",",
                        EscapeCsvField(entry.Timestamp.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture)),
                        EscapeCsvField(entry.Operation),
                        EscapeCsvField(entry.Device),
                        EscapeCsvField(entry.Result),
                        EscapeCsvField(entry.Duration.ToString(@"hh\:mm\:ss\.fff", CultureInfo.InvariantCulture))));
                }
                File.WriteAllText(path, sb.ToString());
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Failed to export operation history to CSV.");
                MessageBox.Show(
                    "Failed to export history to CSV. See log for details.",
                    "Export History Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        private static string EscapeCsvField(string field)
        {
            field = field ?? string.Empty;
            if (field.IndexOfAny(new[] { ',', '"', '\n', '\r' }) >= 0)
                return "\"" + field.Replace("\"", "\"\"") + "\"";
            return field;
        }
    }
}
