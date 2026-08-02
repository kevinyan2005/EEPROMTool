using System.Collections.Specialized;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using OneWire.UI.Wpf.ViewModels;

namespace OneWire.UI.Wpf.Views
{
    public partial class OperationHistoryView : UserControl
    {
        private ObservableCollection<OperationHistoryEntry> _subscribedEntries;

        public OperationHistoryView()
        {
            InitializeComponent();
            DataContextChanged += OnDataContextChanged;
        }

        private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (_subscribedEntries != null)
                _subscribedEntries.CollectionChanged -= OnEntriesChanged;

            _subscribedEntries = (DataContext as MainViewModel)?.History.Entries;

            if (_subscribedEntries != null)
                _subscribedEntries.CollectionChanged += OnEntriesChanged;
        }

        private void OnEntriesChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            if (e.Action != NotifyCollectionChangedAction.Add || HistoryDataGrid.Items.Count == 0)
                return;

            Dispatcher.InvokeAsync(() =>
            {
                var lastItem = HistoryDataGrid.Items[HistoryDataGrid.Items.Count - 1];
                HistoryDataGrid.ScrollIntoView(lastItem);
            });
        }
    }
}
