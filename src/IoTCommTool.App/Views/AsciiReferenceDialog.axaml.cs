using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Input.Platform;
using IoTCommTool.App.ViewModels;

namespace IoTCommTool.App.Views;

public partial class AsciiReferenceDialog : Window
{
    public AsciiReferenceDialog()
    {
        InitializeComponent();
        DataContext = new AsciiReferenceViewModel();
    }

    private async void OnRowDoubleTapped(object? sender, TappedEventArgs e)
    {
        if ((sender as Control)?.DataContext is not AsciiEntry entry)
            return;

        var clipboard = TopLevel.GetTopLevel(this)?.Clipboard;
        if (clipboard is not null)
            await clipboard.SetTextAsync(entry.Escape);
    }
}
