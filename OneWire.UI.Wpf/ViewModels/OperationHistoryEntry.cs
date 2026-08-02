using System;

namespace OneWire.UI.Wpf.ViewModels
{
    public class OperationHistoryEntry
    {
        public DateTime Timestamp { get; set; }
        public string Operation { get; set; }
        public string Device { get; set; }
        public string Result { get; set; }
        public TimeSpan Duration { get; set; }
    }
}
