using System.Collections.Specialized;
using System.Windows.Controls;
using ClashWpf.ViewModels;

namespace ClashWpf.Views;

public partial class LogsView : UserControl
{
    private INotifyCollectionChanged? _watched;

    public LogsView()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
    }

    private void OnDataContextChanged(object sender, System.Windows.DependencyPropertyChangedEventArgs e)
    {
        if (_watched != null)
            _watched.CollectionChanged -= OnCollectionChanged;

        _watched = (DataContext as LogsViewModel)?.Lines;
        if (_watched != null)
        {
            _watched.CollectionChanged += OnCollectionChanged;
            ScrollToEnd();
        }
    }

    private void OnCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        => Dispatcher.BeginInvoke(ScrollToEnd);

    private void ScrollToEnd()
    {
        if (LogList.Items.Count == 0) return;
        LogList.ScrollIntoView(LogList.Items[LogList.Items.Count - 1]);
    }
}
