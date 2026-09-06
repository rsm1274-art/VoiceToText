using System.Windows;
using Murmur.Dictionary;

namespace Murmur;

public partial class DictionaryWindow : Window
{
    private readonly CustomDictionaryStore _store;

    public DictionaryWindow(CustomDictionaryStore store)
    {
        InitializeComponent();
        _store = store;
        Loaded += (_, _) => Refresh();
    }

    private void Refresh()
    {
        EntriesList.ItemsSource = _store.GetAll();
    }

    private void OnAddClick(object sender, RoutedEventArgs e)
    {
        var term = TermBox.Text.Trim();
        if (string.IsNullOrEmpty(term))
        {
            return;
        }

        _store.Add(term, AliasesBox.Text.Trim());
        TermBox.Clear();
        AliasesBox.Clear();
        Refresh();
    }

    private void OnDeleteClick(object sender, RoutedEventArgs e)
    {
        if (EntriesList.SelectedItem is DictionaryEntry entry)
        {
            _store.Delete(entry.Id);
            Refresh();
        }
    }
}
