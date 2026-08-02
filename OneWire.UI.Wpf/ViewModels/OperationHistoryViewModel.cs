using System;
using System.Collections.ObjectModel;
using System.Globalization;
using slf4net;

namespace OneWire.UI.Wpf.ViewModels
{
    public class OperationHistoryViewModel : ViewModelBase
    {
        // Dedicated logger name — routed by NLog.config to the "historyfile" target only,
        // independent from the general application log.
        private static ILogger HistoryLogger { get; } = LoggerFactory.GetLogger("OperationHistory");

        public ObservableCollection<OperationHistoryEntry> Entries { get; } = new ObservableCollection<OperationHistoryEntry>();

        public void Add(string operation, string device, string result, TimeSpan duration)
        {
            var timestamp = DateTime.Now;

            Entries.Add(new OperationHistoryEntry
            {
                Timestamp = timestamp,
                Operation = operation,
                Device = device,
                Result = result,
                Duration = duration
            });

            HistoryLogger.Info(string.Join(",",
                EscapeCsvField(timestamp.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture)),
                EscapeCsvField(operation),
                EscapeCsvField(device),
                EscapeCsvField(result),
                EscapeCsvField(duration.ToString(@"hh\:mm\:ss\.fff", CultureInfo.InvariantCulture))));
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
