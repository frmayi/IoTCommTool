using Avalonia.Controls;
using Avalonia.Input;

namespace IoTCommTool.App.Views;

public partial class HistorySidebar : UserControl
{
    public HistorySidebar()
    {
        InitializeComponent();
    }

    public void FocusSearch()
    {
        HistorySearchBox.Focus();
        HistorySearchBox.SelectAll();
    }

    private void OnSearchKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key != Key.Escape)
            return;

        if (!string.IsNullOrEmpty(HistorySearchBox.Text))
        {
            HistorySearchBox.Text = string.Empty;
            e.Handled = true;
        }
    }
}
