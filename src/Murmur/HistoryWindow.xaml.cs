using System.Windows;
using Murmur.History;

namespace Murmur;

public partial class HistoryWindow : Window
{
    private readonly HistoryStore _store;

    public HistoryWindow(HistoryStore store)
    {
        InitializeComponent();
        _store = store;
        Loaded += OnLoaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (!_store.IsAvailable)
        {
            StatusText.Text = "History unavailable: could not open the local database " +
                               "(check disk space and permissions under %AppData%\\Murmur).";
            return;
        }

        var rows = _store.GetAll()
            .Select(r => new HistoryRow(r.TimestampUtc.ToLocalTime().ToString("g"), r.Text))
            .ToList();

        StatusText.Text = rows.Count == 0
            ? "No transcriptions yet."
            : $"{rows.Count} transcription(s).";
        HistoryList.ItemsSource = rows;
    }

    private sealed record HistoryRow(string LocalTimestampDisplay, string Text);
}
